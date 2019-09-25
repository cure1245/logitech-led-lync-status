using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using Microsoft.Lync.Utilities;
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
                new MenuItem("Exit", (s, e) => { Application.Exit(); }),
            });
                icon.Visible = true;

                
                lyncClient.StateChanged += LyncClient_StateChanged;
                ManualResetEventSlim _isRunning;
                Application.Run();
                icon.Visible = false;
            }
        }

        private static void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case ClientState.SignedIn:
                    LogitechGSDK.LogiLedInit();
                    self = lyncClient.Self;
                    self.Contact.ContactInformationChanged += OwnInfoHasChanged;
                    lyncClient.ConversationManager.ConversationAdded += ConversationAdded;
                    lyncClient.ConversationManager.ConversationRemoved += ConversationRemoved;
                    SetHeadsetLEDs((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability));
                    break;
                case ClientState.SigningOut:
                    self = null;
                    LogitechGSDK.LogiLedShutdown();
                    break;
                case ClientState.ShuttingDown:
                    break;
            }         
        }

        private static void ConversationRemoved(object sender, ConversationManagerEventArgs e)
        {
            LogitechGSDK.LogiLedStopEffects();
        }

        private static void ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            var avModality = e.Conversation.Modalities[ModalityTypes.AudioVideo];
            if (avModality.State == ModalityState.Notified)
            {
                LogitechGSDK.LogiLedFlashLighting(0, 0, 100, LogitechGSDK.LOGI_LED_DURATION_INFINITE, 200);
            }

            e.Conversation.Modalities[ModalityTypes.AudioVideo].ModalityStateChanged += CallStateChanged;
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
            if (self != null && e.ChangedContactInformation.Contains(ContactInformationType.Availability))
                SetHeadsetLEDs((ContactAvailability)self.Contact.GetContactInformation(ContactInformationType.Availability));
        }

        private static void SetHeadsetLEDs(ContactAvailability availability)
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