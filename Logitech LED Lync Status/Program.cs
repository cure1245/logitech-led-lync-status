using System;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;

namespace LyncStatusforRGBDevices
{
    static class Program
    {
        static LedSdk currentSdk = LedSdk.Logitech;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (NotifyIcon icon = new NotifyIcon())
            using (MenuItem reload = new MenuItem("Reload", (s, e) => { ResetStatusWatcher(); }))
            using (MenuItem exit = new MenuItem("Exit", (s, e) => { Application.Exit(); }))
            {
                icon.Icon = Properties.Resources.Icon0;
                icon.ContextMenu = new ContextMenu(new MenuItem[] { reload, exit });
                icon.Visible = true;

                LyncStatusWatcher.AvailabilityChanged += SetLEDToCurrentStatus;
                LyncStatusWatcher.CallStateChanged += CallStatusUpdated;
                LyncStatusWatcher.MessageStateChanged += MsgStatusUpdated;
                LyncStatusWatcher.MessageReceived += MsgReceived;
                ResetStatusWatcher();
                Application.Run();
                icon.Visible = false;
            }
        }

        private static void MsgReceived(object sender, EventArgs e)
        {
            if (LyncStatusWatcher.CurrentCallState == CallState.NoUpdate)
                LedSdkAbstraction.FlashLighting(currentSdk, 0, 0, 100, 2000, 200);
        }
        static void ResetStatusWatcher()
        {            
            LedSdkAbstraction.Shutdown(currentSdk);
            LedSdkAbstraction.Initialize(currentSdk, "Skype for Business Status");
            try
            {
                LyncStatusWatcher.InitializeClient();
            }
            catch (ClientWatcherException)
            {
                var result = MessageBox.Show("Error accessing Lync client!", "Error", MessageBoxButtons.RetryCancel);
                if (result == DialogResult.Cancel) Environment.Exit(1);
                else ResetStatusWatcher();
            }
        }
        private static void MsgStatusUpdated(MessageState state)
        {
            if (state == MessageState.New && LyncStatusWatcher.CurrentCallState == CallState.NoUpdate)
                LedSdkAbstraction.FlashLighting(currentSdk, 0, 0, 100, 2000, 200);
            ThreadPool.QueueUserWorkItem(FlashForWaitingMsg);
        }
        private static void FlashForWaitingMsg(object state)
        {
            bool stillWaiting;
            do
            {
                Thread.Sleep(10000);
                stillWaiting = LyncStatusWatcher.CurrentMsgState == MessageState.New;
                if (stillWaiting && LyncStatusWatcher.CurrentCallState == CallState.NoUpdate)
                    LedSdkAbstraction.FlashLighting(currentSdk, 0, 0, 100, 1000, 200);
            } while (stillWaiting);
        }
        private static void CallStatusUpdated(CallState state)
        {
            switch (state)
            {
                case CallState.Ringing:
                    LedSdkAbstraction.FlashLighting(currentSdk, 0, 0, 100, 120000, 200);
                    break;
                case CallState.Connected:
                    LedSdkAbstraction.PulseLighting(currentSdk, 100, 0, 0, Int32.MaxValue, 800);
                    break;
                case CallState.NoUpdate:
                    SetLEDToCurrentStatus(LyncStatusWatcher.UserStatus);
                    break;
            }
        }
        static void SetLEDToCurrentStatus(Availability availability)
        {
            if (LyncStatusWatcher.CurrentCallState == CallState.Connected) return;
            switch (availability)
            {
                case Availability.DoNotDisturb:
                    LedSdkAbstraction.SetLighting(currentSdk, 100, 0, 100);
                    break;
                case Availability.Free:
                    LedSdkAbstraction.SetLighting(currentSdk, 0, 100, 0);
                    break;
                case Availability.Busy:
                    LedSdkAbstraction.SetLighting(currentSdk, 100, 0, 0);
                    break;
                case Availability.Away:
                case Availability.Idle:
                    LedSdkAbstraction.SetLighting(currentSdk, 100, 75, 0);
                    break;
            }
        }
    }
}