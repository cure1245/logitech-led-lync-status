using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace LyncStatusforRGBDevices
{
    public enum CallState { Ringing, Connected, Ended }
    public enum Availability { Free, Busy, Away, Idle, DoNotDisturb }
    public enum MessageState
    {
        New,
        Updated,
        Opened
    }

    public delegate void CallStateHandler(CallState state);
    public delegate void AvailabilityHandler(Availability availability);
    public delegate void InstantMessageHandler(MessageState state);

    class LyncStatusWatcher
    {
        private LyncClient lyncClient;
        private Self self;
        private static readonly List<ConversationWatcher> watcherList = new List<ConversationWatcher>();

        public static event AvailabilityHandler AvailabilityChanged;
        public static event InstantMessageHandler MessageReceived;
        public static event CallStateHandler CallStatusChanged;
        public event EventHandler OnClientReady;

        private static MessageState currentMsgState;
        public static MessageState CurrentMsgState
        {
            get => currentMsgState;
            private set
            {
                currentMsgState = value;
                MessageReceived?.Invoke(value);
            }
        }

        private bool isClientConnected;
        public bool IsClientConnected
        {
            get => isClientConnected;
            set
            {
                isClientConnected = value;
                OnClientReady(this, new EventArgs());
            }
        }
        private Availability userStatus;
        public Availability UserStatus
        {
            get => userStatus;
            private set
            {
                userStatus = value;
                AvailabilityChanged?.Invoke(value);
            }
        }
        public void InitializeClient()
        {
            try
            {
                do
                {
                    lyncClient = null;
                    lyncClient = LyncClient.GetClient();
                } while (lyncClient.State == ClientState.Invalid);
            }
            catch (ClientNotFoundException)
            {
                var result = MessageBox.Show("Skype for Business is not running!", "Error accessing Lync client", MessageBoxButtons.RetryCancel);
                if (result == DialogResult.Cancel) Environment.Exit(1);
                else InitializeClient();
                return;
            }
            catch (LyncClientException e)
            {
                var result = MessageBox.Show(e.Message, "Error accessing Lync client", MessageBoxButtons.RetryCancel);
                if (result == DialogResult.Cancel) Environment.Exit(1);
                else InitializeClient();
                return;
            }
            SubscribeToClientEvents();
            if (lyncClient.State == ClientState.SignedIn) DoLoginTasks();
            IsClientConnected = true;
        }
        private void SubscribeToClientEvents()
        {
            lyncClient.StateChanged += LyncClient_StateChanged;
            lyncClient.ConversationManager.ConversationAdded += ConversationAdded;
            //lyncClient.ConversationManager.ConversationRemoved += ConversationRemoved;
        }
        private void SubscribeToSelfEvents()
        {
            self = lyncClient.Self;
            self.Contact.ContactInformationChanged += OwnInfoHasChanged;
        }
        private void DoLoginTasks()
        {
            foreach (var c in lyncClient.ConversationManager.Conversations)
            {
                watcherList.Add(new ConversationWatcher(c));
            }
            SubscribeToSelfEvents();
            SetAvailability();
        }
        private void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case ClientState.SignedIn:
                    DoLoginTasks();
                    break;
                case ClientState.SigningOut:
                    //TODO: Signing out logic.
                    break;
            }
        }
        private void ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            watcherList.Add(new ConversationWatcher(e.Conversation));
        }        
        private static void CallModalityUpdated(object sender, ModalityStateChangedEventArgs e)
        {
            if (e.NewState == ModalityState.Connected) CallStatusChanged?.Invoke(CallState.Connected);
            else if (e.NewState == ModalityState.Disconnected) CallStatusChanged?.Invoke(CallState.Ended);
        }
        private void OwnInfoHasChanged(object sender, ContactInformationChangedEventArgs e)
        {
            if (self.Contact != null && e.ChangedContactInformation.Contains(ContactInformationType.Availability))
                if (AvailabilityChanged != null) SetAvailability();
        }
        private void SetAvailability()
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

        private class ConversationWatcher
        {
            private readonly InstantMessageModality im;
            private readonly AVModality call;
            private readonly ManualResetEventSlim msgAck = new ManualResetEventSlim(true);
            private readonly Conversation conversation;

            public bool IsAcknowledged { get; set; }

            public event EventHandler OnMsgAck;

            public ConversationWatcher(Conversation c)
            {
                conversation = c;
                im = (InstantMessageModality)c.Modalities[ModalityTypes.InstantMessage];
                call = (AVModality)c.Modalities[ModalityTypes.AudioVideo];
                ThreadPool.QueueUserWorkItem(WatcherThread);
            }
            private void WatcherThread(object o)
            {
                conversation.ConversationManager.ConversationRemoved += ConversationRemoved;

                if (call.State == ModalityState.Notified) CallStatusChanged?.Invoke(CallState.Ringing);

                else if (im.State == ModalityState.Notified)
                {
                    msgAck.Reset();
                    SetMessageAsNew();
                }
                foreach (var p in conversation.Participants)
                {
                    if (!p.IsSelf && !p.IsMuted)
                    {
                        var p_im = (InstantMessageModality)p.Modalities[ModalityTypes.InstantMessage];
                        var p_call = (AVModality)p.Modalities[ModalityTypes.AudioVideo];

                        p_im.InstantMessageReceived += Im_InstantMessageReceived;
                        p_im.ModalityStateChanged += WaitForNewMessage;
                        p_call.ModalityStateChanged += CallModalityUpdated;
                    }
                }                
            }

            private void SetMessageAsNew()
            {                
                im.ModalityStateChanged += WaitForReceipt;
                MessageReceived?.Invoke(MessageState.New);
                msgAck.Wait();
                MessageReceived?.Invoke(MessageState.Opened);
            }

            private void ConversationRemoved(object sender, ConversationManagerEventArgs e)
            {
                if (e.Conversation == this.conversation)
                {
                    conversation.ConversationManager.ConversationRemoved -= ConversationRemoved;
                    watcherList.Remove(this);
                }
            }
            private void WaitForReceipt(object sender, ModalityStateChangedEventArgs e)
            {
                msgAck.Set();
                im.ModalityStateChanged -= WaitForReceipt;
            }
            private void WaitForNewMessage(object sender, ModalityStateChangedEventArgs e)
            {
                if (e.NewState == ModalityState.Notified) SetMessageAsNew();
            }
            private static void Im_InstantMessageReceived(object sender, MessageSentEventArgs e)
            {
                MessageReceived?.Invoke(MessageState.Updated);
            }
        }
    }
}
