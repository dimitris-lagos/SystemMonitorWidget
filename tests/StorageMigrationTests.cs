using System;
using System.IO;
using System.Reflection;

internal static class StorageMigrationTests
{
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Expected widget EXE and output root.");
        string root = Path.Combine(Path.GetFullPath(args[1]), "migration-test-" + Guid.NewGuid().ToString("N"));
        string legacy = Path.Combine(root, "VegaDesktopWidget"), current = Path.Combine(root, "SystemMonitorWidget");
        Directory.CreateDirectory(Path.Combine(legacy, "Runtime", "old"));
        File.WriteAllText(Path.Combine(legacy, "settings.ini"), "HeaderTitle=MY SYSTEM");
        File.WriteAllText(Path.Combine(legacy, "HWiNFO-autorestart.log"), "log");
        File.WriteAllText(Path.Combine(legacy, "Runtime", "old", "helper.exe"), "helper");
        Type config = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("VegaDesktopWidget.WidgetConfig", true);
        MethodInfo migrate = config.GetMethod("MigrateDirectory", BindingFlags.Static | BindingFlags.NonPublic);
        migrate.Invoke(null, new object[] { legacy, current });
        if (Directory.Exists(legacy)) throw new Exception("Legacy folder was not deleted.");
        foreach (string relative in new string[] { "settings.ini", "HWiNFO-autorestart.log", Path.Combine("Runtime", "old", "helper.exe") })
            if (!File.Exists(Path.Combine(current, relative))) throw new Exception("Missing migrated file: " + relative);
        Console.WriteLine("StorageMigration=PASS Files=3 LegacyDeleted=PASS");
    }
}
