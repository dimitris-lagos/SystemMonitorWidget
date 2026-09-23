using System;
using System.Threading;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (UpdateService.IsInstallerCommand(args))
            {
                UpdateService.RunInstaller(args);
                return;
            }
            bool created;
            using (Mutex singleInstance = new Mutex(true, "Local\\VegaDesktopWidget", out created))
            {
                if (!created) return;
                WidgetConfig.MigrateLegacyStorage();
                try { EmbeddedSupportFiles.PrepareAndCleanLegacyInstallFiles(); }
                catch (Exception ex)
                {
                    try { System.IO.Directory.CreateDirectory(WidgetConfig.Folder); System.IO.File.AppendAllText(System.IO.Path.Combine(WidgetConfig.Folder, "runtime-error.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + ex + Environment.NewLine); }
                    catch { }
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WidgetForm());
            }
        }
    }
}
