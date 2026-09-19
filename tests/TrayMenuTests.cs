using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

internal static class TrayMenuTests
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected widget EXE path.");
        Assembly assembly = Assembly.LoadFrom(args[0]);
        Type formType = assembly.GetType("VegaDesktopWidget.WidgetForm", true);
        Type configType = assembly.GetType("VegaDesktopWidget.WidgetConfig", true);
        object form = FormatterServices.GetUninitializedObject(formType);
        formType.GetField("config", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(form, Activator.CreateInstance(configType, true));
        formType.GetMethod("BuildMenu", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(form, null);
        ContextMenuStrip menu = (ContextMenuStrip)formType.GetField("menu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        ToolStripMenuItem visibility = (ToolStripMenuItem)formType.GetField("visibilityItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        try
        {
            if (menu.Items[0].Text != "Configure dashboard…" || menu.Items[1].Text != "Refresh now")
                throw new Exception("Original gear actions are missing.");
            if (menu.Items[menu.Items.Count - 1].Text != "Exit" || menu.Items.IndexOf(visibility) < 0)
                throw new Exception("Tray menu actions are missing.");
            if (visibility.Available) throw new Exception("Show/Hide must not appear before tray opens.");
            MethodInfo opening = formType.GetMethod("MenuOpening", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo gearFlag = formType.GetField("openingFromGear", BindingFlags.NonPublic | BindingFlags.Instance);
            opening.Invoke(form, new object[] { menu, new CancelEventArgs() });
            if (!visibility.Available || visibility.Text != "Show widget") throw new Exception("Tray menu must offer Show when hidden.");
            gearFlag.SetValue(form, true);
            opening.Invoke(form, new object[] { menu, new CancelEventArgs() });
            if (visibility.Available) throw new Exception("Gear menu must not show tray-only action.");
            Console.WriteLine("SharedTrayMenu=PASS TrayOnlyVisibilityAction=PASS");
        }
        finally { menu.Dispose(); }
    }
}
