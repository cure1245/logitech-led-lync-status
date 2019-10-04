using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace LyncStatusforRGBDevices
{
    public enum CallState { Ringing, Connected, NoUpdate }
    public enum Availability { Free, Busy, Away, Idle, DoNotDisturb }
    public enum MessageState { New, Updated, NoUpdate }

    public delegate void CallStateHandler(CallState state);
    public delegate void AvailabilityHandler(Availability availability);
    public delegate void InstantMessageHandler(MessageState state);

    class LyncStatusWatcher
    {
        private LyncClient lyncClient;
        private Self self;
        private static readonly List<ConversationWatcher> watcherList = new List<ConversationWatcher>();

        public static event AvailabilityHandler AvailabilityChanged;
        public static event InstantMessageHandler MessageStateChanged;
        public static event CallStateHandler CallStateChanged;
        public static event EventHandler MessageReceived;
        public event EventHandler ClientIsReady;

        private static MessageState currentMsgState = MessageState.NoUpdate;
        public static MessageState CurrentMsgState
        {
            get => currentMsgState;
            private set
            {
                currentMsgState = value;
                MessageStateChanged?.Invoke(value);
            }
        }
        private static CallState currentCallState = CallState.NoUpdate;
        public static CallState CurrentCallState
        {
            get => currentCallState;
            set
            {
                currentCallState = value;
                CallStateChanged?.Invoke(value);
            }
        }

        private bool isClientConnected;
        public bool IsClientConnected
        {
            get => isClientConnected;
            set
            {
                isClientConnected = value;
            }
        }
        private static Availability userStatus;
        public static Availability UserStatus
        {
            get => userStatus;
            private set
            {
                userStatus = value;
                AvailabilityChanged?.Invoke(value);
            }
        }
        public bool InitializeClient()
        {

        //TODO: Move references to Forms to calling application.
            bool rdy;
            try
            {
                do
                {
                    lyncClient = null;
                    lyncClient = LyncClient.GetClient();
                } while (lyncClient.State == ClientState.Invalid);

                IsClientConnected = true;
            }
            catch (ClientNotFoundException)
            {
                var result = MessageBox.Show("Skype for Business is not running!", "Error accessing Lync client", MessageBoxButtons.RetryCancel);
                if (result == DialogResult.Cancel) rdy = false;
                else rdy = InitializeClient();
                return rdy;
            }
            catch (LyncClientException e)
            {
                var result = MessageBox.Show(e.Message, "Error accessing Lync client", MessageBoxButtons.RetryCancel);
                if (result == DialogResult.Cancel) rdy = false;
                else rdy = InitializeClient();
                return rdy;
            }
            SubscribeToClientEvents();
            if (lyncClient.State == ClientState.SignedIn) DoLoginTasks();
            rdy = true;
            return rdy;
        }
        private void SubscribeToClientEvents()
        {
            lyncClient.StateChanged += LyncClient_StateChanged;
            lyncClient.ConversationManager.ConversationAdded += ConversationAdded;
        }
        private void SubscribeToSelfEvents()
        {
            self = lyncClient.Self;
            self.Contact.ContactInformationChanged += OwnInfoHasChanged;
        }
        private void DoLoginTasks()
        {
            foreach (var c in lyncClient.ConversationManager.Conversations)
                watcherList.Add(new ConversationWatcher(c));

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
                    watcherList.Clear();
                    break;
            }
        }
        private void ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            watcherList.Add(new ConversationWatcher(e.Conversation));
        }
        private static void CallModalityUpdated(object sender, ModalityStateChangedEventArgs e)
        {
            if (e.NewState == ModalityState.Connected) CurrentCallState = CallState.Connected;
            else if (e.NewState == ModalityState.Disconnected) CurrentCallState = CallState.NoUpdate;
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

        private class ConversationWatcher : IDisposable
        {
            private readonly InstantMessageModality im;
            private readonly AVModality call;
            private readonly ManualResetEventSlim msgAck = new ManualResetEventSlim(true);
            private readonly Conversation conversation;

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
                im.ModalityStateChanged += MsgNotification;
                call.ModalityStateChanged += CallNotification;

                //if (call.State == ModalityState.Notified) CurrentCallState = CallState.Ringing;

                //else if (im.State == ModalityState.Notified)
                //{
                //    msgAck.Reset();
                //    SetMessageAsNew();
                //}
                foreach (var p in conversation.Participants)
                {
                    if (!p.IsSelf && !p.IsMuted)
                    {
                        var p_im = (InstantMessageModality)p.Modalities[ModalityTypes.InstantMessage];
                        var p_call = (AVModality)p.Modalities[ModalityTypes.AudioVideo];

                        p_im.InstantMessageReceived += (b, i) => MessageReceived(b, i);
                        p_call.ModalityStateChanged += CallModalityUpdated;
                    }
                }
            }

            private void CallNotification(object sender, ModalityStateChangedEventArgs e)
            {
                if (e.NewState == ModalityState.Notified) CurrentCallState = CallState.Ringing;
                else if (e.NewState == ModalityState.Connected) CurrentCallState = CallState.Connected;
                else if (e.NewState == ModalityState.Disconnected) CurrentCallState = CallState.NoUpdate;
            }

            private void SetMessageAsNew(object state)
            {
                im.ModalityStateChanged += WaitForMsgReceipt;
                CurrentMsgState = MessageState.New;
                msgAck.Wait();
                CurrentMsgState = MessageState.NoUpdate;
            }
            private void ConversationRemoved(object sender, ConversationManagerEventArgs e)
            {
                if (e.Conversation == this.conversation)
                {
                    conversation.ConversationManager.ConversationRemoved -= ConversationRemoved;
                    watcherList.Remove(this);
                }
            }
            private void WaitForMsgReceipt(object sender, ModalityStateChangedEventArgs e)
            {
                if (e.NewState == ModalityState.Connected)
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

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // TODO: dispose managed state (managed objects).
                        msgAck.Dispose();
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            // ~ConversationWatcher()
            // {
            //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            //   Dispose(false);
            // }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                // GC.SuppressFinalize(this);
            }
            #endregion
        }
    }
}
