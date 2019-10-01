using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using LedCSharp;
using System.Threading;

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
#pragma warning disable IDE0067 // Dispose objects before losing scope
                    new MenuItem("Reload", (s, e) => { ResetStatusWatcher(); }),
#pragma warning restore IDE0067 // Dispose objects before losing scope
                    new MenuItem("Exit", (s, e) => { Application.Exit(); }),
            });
                icon.Visible = true;
                
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