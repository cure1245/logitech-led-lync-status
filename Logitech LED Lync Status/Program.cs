using LedCSharp;
using System;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;

namespace LyncStatusforRGBDevices
{
    static class Program
    {
        static LyncStatusWatcher statusWatcher;
        static readonly ManualResetEvent clientReady = new ManualResetEvent(false);
        static bool msgWaiting = false;

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

                ResetStatusWatcher();
                Application.Run();
                icon.Visible = false;
            }
        }
        static void ResetStatusWatcher()
        {            
            statusWatcher = new LyncStatusWatcher();
            statusWatcher.OnClientReady += StatusWatcher_OnClientReady;
            statusWatcher.InitializeClient();
            clientReady.WaitOne();
            LogitechGSDK.LogiLedInitWithName("Skype for Business Status");
            LyncStatusWatcher.AvailabilityChanged += SetLEDToCurrentStatus;
            LyncStatusWatcher.CallStatusChanged += StatusWatcher_CallStatusChanged;
            LyncStatusWatcher.MessageReceived += StatusWatcher_MessageReceived;
            SetLEDToCurrentStatus(statusWatcher.UserStatus);
        }

        private static void StatusWatcher_MessageReceived(MessageState state)
        {
            switch (state)
            {
                case MessageState.New:
                    LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 2000, 200);
                    if (msgWaiting) return;
                    msgWaiting = true;
                    ThreadPool.QueueUserWorkItem(FlashForWaitingMsg);
                    break;
                case MessageState.Updated:
                    LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 2000, 200);
                    break;
            }
        }

        private static void FlashForWaitingMsg(object state)
        {
            while (LyncStatusWatcher.CurrentMsgState == MessageState.New)
            {
                Thread.Sleep(10000);
                LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 1000, 200);
                SetLEDToCurrentStatus(statusWatcher.UserStatus);
            }
            msgWaiting = false;
        }

        private static void StatusWatcher_OnMsgAck(object sender, EventArgs e)
        {
            msgWaiting = false;
        }

        private static void StatusWatcher_OnClientReady(object sender, EventArgs e)
        {
            clientReady.Set();
        }

        private static void StatusWatcher_CallStatusChanged(CallState state)
        {
            switch (state)
            {
                case CallState.Ringing:
                    LogitechGSDK.LogiLedFlashLighting(0, 0, 100, 120000, 200);
                    break;
                case CallState.Connected:
                    LogitechGSDK.LogiLedPulseLighting(100, 0, 0, Int32.MaxValue, 800);
                    break;
                case CallState.Ended:
                    SetLEDToCurrentStatus(statusWatcher.UserStatus);
                    break;
            }
        }

        static void SetLEDToCurrentStatus(Availability availability)
        {
            switch (availability)
            {
                case Availability.DoNotDisturb:
                    LogitechGSDK.LogiLedSetLighting(100, 0, 100);
                    break;
                case Availability.Free:
                    LogitechGSDK.LogiLedSetLighting(0, 100, 0);
                    break;
                case Availability.Busy:
                    LogitechGSDK.LogiLedSetLighting(100, 0, 0);
                    break;
                case Availability.Away:
                case Availability.Idle:
                    LogitechGSDK.LogiLedSetLighting(100, 75, 0);
                    break;
            }
        }
    }
}