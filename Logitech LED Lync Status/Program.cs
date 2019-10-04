using System;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;

namespace LyncStatusforRGBDevices
{
    static class Program
    {
        static LyncStatusWatcher statusWatcher;

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
            LedSdkAbstraction.FlashLighting(Sdk.Corsair, 0, 0, 100, 2000, 200);
        }
        static void ResetStatusWatcher()
        {            
            statusWatcher = new LyncStatusWatcher();
            LedSdkAbstraction.Initialize(Sdk.Corsair, "Skype for Business Status");            
            statusWatcher.InitializeClient();
            //SetLEDToCurrentStatus(LyncStatusWatcher.UserStatus);
        }
        private static void MsgStatusUpdated(MessageState state)
        {
            switch (state)
            {
                case MessageState.New:
                    LedSdkAbstraction.FlashLighting(Sdk.Corsair, 0, 0, 100, 2000, 200);                    
                    ThreadPool.QueueUserWorkItem(FlashForWaitingMsg);
                    break;
                case MessageState.Updated:
                    LedSdkAbstraction.FlashLighting(Sdk.Corsair, 0, 0, 100, 2000, 200);
                    break;
            }
        }
        private static void FlashForWaitingMsg(object state)
        {
            while (LyncStatusWatcher.CurrentMsgState == MessageState.New)
            {
                Thread.Sleep(10000);
                if (LyncStatusWatcher.CurrentMsgState == MessageState.New)
                    LedSdkAbstraction.FlashLighting(Sdk.Corsair, 0, 0, 100, 1000, 200);
                SetLEDToCurrentStatus(LyncStatusWatcher.UserStatus);
            }
        }
        private static void CallStatusUpdated(CallState state)
        {
            switch (state)
            {
                case CallState.Ringing:
                    LedSdkAbstraction.FlashLighting(Sdk.Corsair, 0, 0, 100, 120000, 200);
                    break;
                case CallState.Connected:
                    LedSdkAbstraction.PulseLighting(Sdk.Corsair, 100, 0, 0, Int32.MaxValue, 800);
                    break;
                case CallState.NoUpdate:
                    LedSdkAbstraction.Shutdown(Sdk.Corsair);
                    SetLEDToCurrentStatus(LyncStatusWatcher.UserStatus);
                    break;
            }
        }
        static void SetLEDToCurrentStatus(Availability availability)
        {
            switch (availability)
            {
                case Availability.DoNotDisturb:
                    LedSdkAbstraction.SetLighting(Sdk.Corsair, 100, 0, 100);
                    break;
                case Availability.Free:
                    LedSdkAbstraction.SetLighting(Sdk.Corsair, 0, 100, 0);
                    break;
                case Availability.Busy:
                    LedSdkAbstraction.SetLighting(Sdk.Corsair, 100, 0, 0);
                    break;
                case Availability.Away:
                case Availability.Idle:
                    LedSdkAbstraction.SetLighting(Sdk.Corsair, 100, 75, 0);
                    break;
            }
        }
    }
}