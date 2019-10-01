using LedCSharp;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using System;
using System.Threading;
using System.Windows.Forms;

namespace LyncStatusforRGBDevices
{
    public enum CallState { Ringing, Connected, Ended }
    public enum Availability { Free, Busy, Away, Idle, DoNotDisturb }
    public enum MessageState { New, Updated }

    public delegate void CallStateHandler(CallState state);
    public delegate void AvailabilityHandler(Availability availability);
    public delegate void InstantMessageHandler(MessageState state);

    class LyncStatusWatcher
    {
        private LyncClient lyncClient;
        private Self self;

        public bool IsClientConnected { get; private set; }

        public event AvailabilityHandler AvailabilityChanged;
        public event InstantMessageHandler MessageReceived;
        public event CallStateHandler CallStatusChanged;

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
                var result = MessageBox.Show("Skype for Business is not running!", "Client not found", MessageBoxButtons.RetryCancel);
                if (result == DialogResult.Cancel) Environment.Exit(1);
                else InitializeClient();
                return;
            }
            SubscribeToClientEvents();
            if (lyncClient.State == ClientState.SignedIn) DoLoginTasks();
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
                foreach (var p in c.Participants)
                {
                    if (!p.IsSelf && !p.IsMuted)
                    {
                        InstantMessageModality im = (InstantMessageModality)p.Modalities[ModalityTypes.InstantMessage];
                        AVModality call = (AVModality)p.Modalities[ModalityTypes.AudioVideo];

                        im.InstantMessageReceived += Im_InstantMessageReceived;
                        call.ModalityStateChanged += CallStateChanged;
                    }
                }
            }
            LogitechGSDK.LogiLedInitWithName("Skype for Business Status Watcher");
            SubscribeToSelfEvents();
            SetLEDToCurrentStatus();
        }
        private void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case ClientState.SignedIn:
                    DoLoginTasks();
                    break;
                case ClientState.SigningOut:
                    LogitechGSDK.LogiLedShutdown();
                    break;
            }
        }
        private void ConversationRemoved(object sender, ConversationManagerEventArgs e)
        {
            //e.Conversation.Participants
        }
        private void ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            var call = (AVModality)e.Conversation.Modalities[ModalityTypes.AudioVideo];
            var im = (InstantMessageModality)e.Conversation.Modalities[ModalityTypes.InstantMessage];

            if (call.State == ModalityState.Notified)
            {
                CallStatusChanged?.Invoke(CallState.Ringing);
                LogitechGSDK.LogiLedFlashLighting(0, 0, 100, LogitechGSDK.LOGI_LED_DURATION_INFINITE, 200);
                e.Conversation.Modalities[ModalityTypes.AudioVideo].ModalityStateChanged += CallStateChanged;
                return;
            }
            if (im.State == ModalityState.Notified)
            {
                MessageReceived?.Invoke(MessageState.New);
                LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 2000, 200);
                while (im.State == ModalityState.Notified)
                {
                    Thread.Sleep(5000);
                    LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 1000, 250);
                    SetLEDToCurrentStatus();
                }
            }
            foreach (var p in e.Conversation.Participants)
            {
                if (!p.IsSelf && !p.IsMuted)
                {
                    im = (InstantMessageModality)p.Modalities[ModalityTypes.InstantMessage];
                    call = (AVModality)p.Modalities[ModalityTypes.AudioVideo];

                    im.InstantMessageReceived += Im_InstantMessageReceived;
                    call.ModalityStateChanged += CallStateChanged;
                }
            }

        }
        private void Im_InstantMessageReceived(object sender, MessageSentEventArgs e)
        {
            MessageReceived?.Invoke(MessageState.Updated);
            LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 1000, 200);
            SetLEDToCurrentStatus();
        }
        private void CallStateChanged(object sender, ModalityStateChangedEventArgs e)
        {
            if (e.NewState == ModalityState.Connected)
            {
                CallStatusChanged?.Invoke(CallState.Connected);
                LogitechGSDK.LogiLedPulseLighting(100, 0, 0, 7200000, 800);
            }
            else if (e.NewState == ModalityState.Disconnected)
            {
                CallStatusChanged?.Invoke(CallState.Ended);
                LogitechGSDK.LogiLedStopEffects();
            }
        }
        private void OwnInfoHasChanged(object sender, ContactInformationChangedEventArgs e)
        {
            if (self.Contact != null && e.ChangedContactInformation.Contains(ContactInformationType.Availability))
            {
                if (AvailabilityChanged != null)
                {
                    switch ((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability))
                    {
                        case ContactAvailability.DoNotDisturb:
                            AvailabilityChanged(Availability.DoNotDisturb);
                            break;
                        case ContactAvailability.Free:
                            AvailabilityChanged(Availability.Free);
                            break;
                        case ContactAvailability.Busy:
                            AvailabilityChanged(Availability.Busy);
                            break;
                        case ContactAvailability.Away:
                        case ContactAvailability.TemporarilyAway:
                            AvailabilityChanged(Availability.Away);
                            break;
                        case ContactAvailability.FreeIdle:
                        case ContactAvailability.BusyIdle:
                            AvailabilityChanged(Availability.Idle);
                            break;
                    } 
                }
                SetLEDToCurrentStatus();
            }
        }
        private void SetLEDToCurrentStatus()
        {
            switch ((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability))
            {
                case ContactAvailability.DoNotDisturb:
                    LogitechGSDK.LogiLedSetLighting(100, 0, 100);
                    break;
                case ContactAvailability.Free:
                    LogitechGSDK.LogiLedSetLighting(0, 100, 0);
                    break;
                case ContactAvailability.Busy:
                    LogitechGSDK.LogiLedSetLighting(100, 0, 0);
                    break;
                case ContactAvailability.Away:
                case ContactAvailability.FreeIdle:
                case ContactAvailability.BusyIdle:
                case ContactAvailability.TemporarilyAway:
                    LogitechGSDK.LogiLedSetLighting(100, 75, 0);
                    break;
            }

        }
    }
}
