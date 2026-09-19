using System;
using System.Reflection;

internal static class SensorValueFormatterTests
{
    private static void Check(MethodInfo format, double value, string unit, int decimals, bool showUnit, string expected)
    {
        string actual = (string)format.Invoke(null, new object[] { value, unit, decimals, showUnit });
        if (actual != expected) throw new Exception(value + " " + unit + ": expected " + expected + ", got " + actual);
    }

    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected widget EXE path.");
        Assembly assembly = Assembly.LoadFrom(args[0]);
        MethodInfo format = assembly.GetType("VegaDesktopWidget.SensorValueFormatter", true)
            .GetMethod("FormatValue", BindingFlags.Public | BindingFlags.Static);
        Check(format, 1023, "KB/s", -1, true, "1023 KB/s");
        Check(format, 1024, "KB/s", -1, true, "1.00 MB/s");
        Check(format, 1536, "KB/s", -1, true, "1.50 MB/s");
        Check(format, 42530.11, "KB/s", -1, true, "41.53 MB/s");
        Check(format, 1536, "KB/s", 1, true, "1.5 MB/s");
        Check(format, 1536, "KB/s", -1, false, "1.50");
        Check(format, 1536, "Kb/s", -1, true, "1536 Kb/s");
        Check(format, 1536, "MB", -1, true, "1.5 GB");
        Console.WriteLine("NetworkUnitFormatting=PASS (8 cases)");
    }
}
