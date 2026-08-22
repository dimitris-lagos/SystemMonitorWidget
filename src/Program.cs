using System;
using System.Threading;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (Mutex singleInstance = new Mutex(true, "Local\\VegaDesktopWidget", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WidgetForm());
            }
        }
    }
}
