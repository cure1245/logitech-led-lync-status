using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
//using Microsoft.Lync.Controls;
using Microsoft.Lync.Controls.Framework;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using Microsoft.Lync.Utilities;
using LedCSharp;

namespace Logitech_LED_Lync_Status
{
    static class Program
    {
        static LyncClient lyncClient = LyncClient.GetClient();
        static Contact self = lyncClient.Self.Contact;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (NotifyIcon icon = new NotifyIcon())
            {
                icon.Icon = Properties.Resources.Icon1;
                icon.ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Show form", (s, e) => {new Form1().Show();}),
                new MenuItem("Exit", (s, e) => { Application.Exit(); }),
            });
                icon.Visible = true;

                LogitechGSDK.LogiLedInit();

                self.ContactInformationChanged += OwnStatusHasChanged;
                lyncClient.ConversationManager.ConversationAdded += ConversationManager_ConversationAdded;
                lyncClient.ConversationManager.ConversationRemoved += ConversationManager_ConversationRemoved;
                SetHeadsetLEDs((ContactAvailability)self.GetContactInformation(ContactInformationType.Availability), null);

                Application.Run();

                LogitechGSDK.LogiLedShutdown();
                icon.Visible = false;
            }
        }

        private static void ConversationManager_ConversationRemoved(object sender, ConversationManagerEventArgs e)
        {
            LogitechGSDK.LogiLedStopEffects();
        }

        private static void ConversationManager_ConversationAdded(object sender, ConversationManagerEventArgs e)
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

        private static void OwnStatusHasChanged(object sender, ContactInformationChangedEventArgs e)
        {
            if (!(e.ChangedContactInformation.Contains(ContactInformationType.Availability) || e.ChangedContactInformation.Contains(ContactInformationType.ActivityId))) return;

            SetHeadsetLEDs((ContactAvailability)self.GetContactInformation(ContactInformationType.Availability), (string)self.GetContactInformation(ContactInformationType.ActivityId));
        }

        private static void SetHeadsetLEDs(ContactAvailability availability, string activity)
        {
            switch (availability)
            {
                case ContactAvailability.Free:
                    LogitechGSDK.LogiLedSetLighting(0, 100, 0);
                    break;
                case ContactAvailability.Busy:
                case ContactAvailability.BusyIdle:
                    LogitechGSDK.LogiLedSetLighting(100, 0, 0);
                    break;
                case ContactAvailability.Away:
                case ContactAvailability.FreeIdle:
                    LogitechGSDK.LogiLedSetLighting(100, 75, 0);
                    break;
            }

        }
    }
}