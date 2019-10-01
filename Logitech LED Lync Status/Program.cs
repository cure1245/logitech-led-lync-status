using System;
using System.Windows.Forms;
using System.Windows.Threading;

namespace LyncStatusforRGB
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
            {
                icon.Icon = Properties.Resources.Icon0;
                icon.ContextMenu = new ContextMenu(new MenuItem[] {
                    new MenuItem("Reload", (s, e) => { ResetStatusWatcher(); }),
                    new MenuItem("Exit", (s, e) => { Application.Exit(); }),
            });
                icon.Visible = true;

                statusWatcher = new LyncStatusWatcher();
                statusWatcher.InitializeClient();
                Application.Run();
                icon.Visible = false;
            }
        }
        static void ResetStatusWatcher()
        {
            statusWatcher = new LyncStatusWatcher();
            statusWatcher.InitializeClient();
        }
    }
}