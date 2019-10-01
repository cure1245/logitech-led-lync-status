using LedCSharp;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using System;
using System.Threading;
using System.Windows.Forms;

namespace LyncStatusforRGB
{
    class LyncStatusWatcher
    {
        private LyncClient lyncClient;
        private Self self;

        public void InitializeClient()
        {
            try
            {
                lyncClient = null;
                lyncClient = LyncClient.GetClient();
                if (lyncClient.State == ClientState.Invalid)
                {
                    throw new NotImplementedException();
                }
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
        }
        private void SubscribeToClientEvents()
        {
            lyncClient.ClientDisconnected += LyncClient_ClientDisconnected;
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
                        im.InstantMessageReceived += Im_InstantMessageReceived;
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
                case ClientState.ShuttingDown:
                    lyncClient = null;
                    break;
            }
        }
        private void LyncClient_ClientDisconnected(object sender, EventArgs e)
        {
            //TODO: Add client disconnect handling.
            throw new NotImplementedException();
        }
        private void ConversationRemoved(object sender, ConversationManagerEventArgs e)
        {
            //TODO: Add conversation removal logic.
            throw new NotImplementedException();
        }
        private void ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            var call = (AVModality)e.Conversation.Modalities[ModalityTypes.AudioVideo];
            var im = (InstantMessageModality)e.Conversation.Modalities[ModalityTypes.InstantMessage];

            if (call.State == ModalityState.Notified)
            {
                LogitechGSDK.LogiLedFlashLighting(0, 0, 100, LogitechGSDK.LOGI_LED_DURATION_INFINITE, 200);
                e.Conversation.Modalities[ModalityTypes.AudioVideo].ModalityStateChanged += CallStateChanged;
                return;
            }
            if (im.State == ModalityState.Notified)
            {
                LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 2000, 200);
                while (im.State == ModalityState.Notified)
                {
                    Thread.Sleep(5000);
                    LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 1000, 250);
                    //SetLEDToCurrentStatus((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability));
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
            LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 1000, 200);
            SetLEDToCurrentStatus();
        }
        private void CallStateChanged(object sender, ModalityStateChangedEventArgs e)
        {
            if (e.NewState == ModalityState.Connected)
            {
                LogitechGSDK.LogiLedPulseLighting(100, 0, 0, 7200000, 800);
            }
            else if (e.NewState == ModalityState.Disconnected)
            {
                LogitechGSDK.LogiLedStopEffects();
            }
        }
        private void OwnInfoHasChanged(object sender, ContactInformationChangedEventArgs e)
        {
            if (self.Contact != null && e.ChangedContactInformation.Contains(ContactInformationType.Availability))
                SetLEDToCurrentStatus();
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
