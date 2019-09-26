using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using LedCSharp;
using System.Threading;

namespace Logitech_LED_Lync_Status
{
    static class Program
    {
        static LyncClient lyncClient;
        static Self self;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                lyncClient = LyncClient.GetClient();
            }
            catch (ClientNotFoundException)
            {
                var result = MessageBox.Show("Skype for Business is not running!", "Warning", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Retry) Application.Restart();
                else Environment.Exit(1);
            }

            using (NotifyIcon icon = new NotifyIcon())
            {
                icon.Icon = Properties.Resources.Icon1;
                icon.ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Reload", (s, e) => { Application.Restart(); }),
                new MenuItem("Exit", (s, e) => { Application.Exit(); }),
            });
                icon.Visible = true;

                lyncClient.StateChanged += LyncClient_StateChanged;
                if (lyncClient.State == ClientState.SignedIn) Initialize();
                Application.Run();
                icon.Visible = false;
            }
        }

        private static void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case ClientState.SignedIn:
                    Initialize();
                    break;
                case ClientState.SigningOut:
                    LogitechGSDK.LogiLedShutdown();
                    break;
                case ClientState.ShuttingDown:
                    lyncClient = null;
                    break;
            }
        }

        private static void Initialize()
        {
            LogitechGSDK.LogiLedInitWithName("Skype for Business Status Watcher");
            self = lyncClient.Self;
            self.Contact.ContactInformationChanged += OwnInfoHasChanged;
            lyncClient.ConversationManager.ConversationAdded += ConversationAdded;
            lyncClient.ConversationManager.ConversationRemoved += ConversationRemoved;
            SetLEDToCurrentStatus((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability));
        }

        private static void ConversationRemoved(object sender, ConversationManagerEventArgs e)
        {
            LogitechGSDK.LogiLedStopEffects();
        }

        private static void ConversationAdded(object sender, ConversationManagerEventArgs e)
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
                foreach (var p in e.Conversation.Participants)
                {
                    if (!p.IsSelf)
                    {
                        im = (InstantMessageModality)p.Modalities[ModalityTypes.InstantMessage];
                        im.InstantMessageReceived += Im_InstantMessageReceived;
                    }
                }
            }
        }

        private static void Im_InstantMessageReceived(object sender, MessageSentEventArgs e)
        {
            LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 2000, 200);
        }

        private static void CallStateChanged(object sender, ModalityStateChangedEventArgs e)
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

        private static void OwnInfoHasChanged(object sender, ContactInformationChangedEventArgs e)
        {
            if (self.Contact != null && e.ChangedContactInformation.Contains(ContactInformationType.Availability))
                SetLEDToCurrentStatus((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability));
        }

        private static void SetLEDToCurrentStatus(ContactAvailability availability)
        {
            switch (availability)
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