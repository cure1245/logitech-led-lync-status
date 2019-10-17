using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace RgbLyncStatus
{
    public enum CallState { Ringing, Connected, NoUpdate }
    public enum Availability { Unknown, Free, Busy, Away, Idle, DoNotDisturb }
    public enum MessageState { New, Updated, NoUpdate }

    public delegate void CallStateHandler(CallState state);
    public delegate void AvailabilityHandler(Availability availability);
    public delegate void InstantMessageHandler(MessageState state);
    public delegate void ClientStatusHandler(bool clientRunning);

    public static class LyncStatusWatcher
    {
        private static readonly List<ConversationWatcher> watcherList = new List<ConversationWatcher>();
        private static readonly Thread processWatcher;
        private static LyncClient lyncClient;
        private static Self self;
        private static bool clientProcessIsRunning;

        public static event AvailabilityHandler AvailabilityChanged;
        public static event InstantMessageHandler MessageStateChanged;
        public static event CallStateHandler CallStateChanged;
        public static event EventHandler MessageReceived;
        public static event ClientStatusHandler WatcherStatusChanged;

        private static MessageState currentMsgState = MessageState.NoUpdate;
        public static MessageState CurrentMsgState
        {
            get => currentMsgState;
            private set
            {
                if (currentMsgState != value)
                {
                    Debug.WriteLine($"Msg State is now {value}");
                    currentMsgState = value;
                    MessageStateChanged?.Invoke(value);
                }
            }
        }

        private static CallState currentCallState = CallState.NoUpdate;
        public static CallState CurrentCallState
        {
            get => currentCallState;
            private set
            {
                if (currentCallState != value)
                {
                    Debug.WriteLine($"Call state is now {value}");
                    currentCallState = value;
                    CallStateChanged?.Invoke(value);
                }
            }
        }

        private static Availability userStatus = Availability.Unknown;
        public static Availability UserStatus
        {
            get => userStatus;
            private set
            {
                if (userStatus != value)
                {
                    Debug.WriteLine($"Status is now {value}");
                    userStatus = value;
                    AvailabilityChanged?.Invoke(value);
                }
            }
        }


        private static bool watcherIsInitialized = false;
        public static bool WatcherIsInitialized
        {
            get => watcherIsInitialized;
            set
            {
                if (watcherIsInitialized != value)
                {
                    Debug.WriteLine($"WatcherIsInitialized == {value}");
                    watcherIsInitialized = value;
                    WatcherStatusChanged?.Invoke(value);
                }
            }
        }

        static LyncStatusWatcher()
        {
            processWatcher = new Thread(ProcessWatcherLoop)
            {
                Name = "Process Watcher",
                IsBackground = true
            };
            processWatcher.Start();
        }

        private static void ProcessWatcherLoop()
        {
            while (true)
            {
                clientProcessIsRunning = (Process.GetProcessesByName("lync").Length == 0) ? false : true;
                if (!clientProcessIsRunning || !WatcherIsInitialized)
                {
                    if (UserStatus != Availability.Unknown) UserStatus = Availability.Unknown;
                    try
                    {
                        InitializeClient();
                    }
                    catch (ClientWatcherException)
                    {
                        WatcherIsInitialized = false;
                    }
                }
                Thread.Sleep(1000);
            }
        }
        public static bool InitializeClient()
        {
            //try to get a new instance of a running lync client.
            try
            {
                do
                {
                    lyncClient = null;
                    lyncClient = LyncClient.GetClient();
                    if (lyncClient.State == ClientState.Invalid && !clientProcessIsRunning)
                        throw new ClientWatcherException();
                }
                while (lyncClient.State == ClientState.Invalid);
            }
            catch (LyncClientException)
            {
                throw new ClientWatcherException();
            }

            //Subscribe to client events.
            lyncClient.StateChanged += LyncClient_StateChanged;
            lyncClient.ConversationManager.ConversationAdded += Manager_ConversationAdded;
            lyncClient.ConversationManager.ConversationRemoved += Manager_ConversationRemoved;

            if (lyncClient.State == ClientState.SignedIn) DoLoginTasks();
            WatcherIsInitialized = true;
            SetAvailability();
            return WatcherIsInitialized;
        }
        private static void DoLoginTasks()
        {
            foreach (var c in lyncClient.ConversationManager.Conversations)
                watcherList.Add(new ConversationWatcher(c));

            //Subscribe to Self object events.
            self = lyncClient.Self;
            self.Contact.ContactInformationChanged += OwnInfoHasChanged;
        }
        private static void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case ClientState.SignedIn:
                    DoLoginTasks();
                    break;
                case ClientState.SigningOut:
                    watcherList.Clear();
                    break;
            }
        }
        private static void Manager_ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            watcherList.Add(new ConversationWatcher(e.Conversation));
        }
        private static void Manager_ConversationRemoved(object sender, ConversationManagerEventArgs e)
        {
            var removed = watcherList.Find(c => c.Conversation == e.Conversation);
            watcherList.Remove(removed);
            removed.Dispose();
        }
        private static void OwnInfoHasChanged(object sender, ContactInformationChangedEventArgs e)
        {
            if (self.Contact != null && e.ChangedContactInformation.Contains(ContactInformationType.Availability))
                if (AvailabilityChanged != null) SetAvailability();
        }
        private static void SetAvailability()
        {
            switch ((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability))
            {
                case ContactAvailability.DoNotDisturb:
                    UserStatus = Availability.DoNotDisturb;
                    break;
                case ContactAvailability.Free:
                    UserStatus = Availability.Free;
                    break;
                case ContactAvailability.Busy:
                    UserStatus = Availability.Busy;
                    break;
                case ContactAvailability.Away:
                case ContactAvailability.TemporarilyAway:
                    UserStatus = Availability.Away;
                    break;
                case ContactAvailability.FreeIdle:
                case ContactAvailability.BusyIdle:
                    UserStatus = Availability.Idle;
                    break;
            }
        }

        private class ConversationWatcher : IDisposable
        {
            private readonly InstantMessageModality im;
            private readonly AVModality call;
            private readonly ManualResetEventSlim msgAck = new ManualResetEventSlim(true);

            public Conversation Conversation { get; }

            public ConversationWatcher(Conversation c)
            {
                Conversation = c;
                im = (InstantMessageModality)c.Modalities[ModalityTypes.InstantMessage];
                call = (AVModality)c.Modalities[ModalityTypes.AudioVideo];
                ThreadPool.QueueUserWorkItem(WatcherThread);
            }
            private void WatcherThread(object o)
            {
                im.ModalityStateChanged += MsgNotification;
                call.ModalityStateChanged += CallModalityChanged;

                foreach (var p in Conversation.Participants)
                {
                    if (!p.IsSelf && !p.IsMuted)
                    {
                        var p_im = (InstantMessageModality)p.Modalities[ModalityTypes.InstantMessage];
                        p_im.InstantMessageReceived += (b, i) => MessageReceived(b, i);
                    }
                }
            }

            private void CallModalityChanged(object sender, ModalityStateChangedEventArgs e)
            {
                switch (e.NewState)
                {
                    case ModalityState.Notified:
                        CurrentCallState = CallState.Ringing;
                        break;
                    case ModalityState.Connected:
                        CurrentCallState = CallState.Connected;
                        break;
                    case ModalityState.Disconnected:
                        CurrentCallState = CallState.NoUpdate;
                        break;
                }
            }
            private void SetMessageAsNew(object state)
            {
                im.ModalityStateChanged += WaitForMsgReceipt;
                CurrentMsgState = MessageState.New;
                msgAck.Wait();
                CurrentMsgState = MessageState.NoUpdate;
            }
            private void WaitForMsgReceipt(object sender, ModalityStateChangedEventArgs e)
            {
                //if (e.NewState == ModalityState.Connected)
                {
                    msgAck.Set();
                    im.ModalityStateChanged -= WaitForMsgReceipt;
                }
            }
            private void MsgNotification(object sender, ModalityStateChangedEventArgs e)
            {
                if (e.NewState == ModalityState.Notified)
                {
                    msgAck.Reset();
                    ThreadPool.QueueUserWorkItem(SetMessageAsNew);
                }
            }
            public void Dispose()
            {
                if (!msgAck.IsSet) msgAck.Set();
                msgAck.Dispose();
            }
        }
    }

    public class ClientWatcherException : Exception
    {
    }
}
