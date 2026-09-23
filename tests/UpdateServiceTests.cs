using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

internal static class UpdateServiceTests
{
    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static void Invoke(MethodInfo method, params object[] values)
    {
        try { method.Invoke(null, values); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Expected widget EXE, release ZIP, and output root.");
        string executable = Path.GetFullPath(args[0]);
        Assembly widgetAssembly = Assembly.LoadFrom(executable);
        foreach (string resourceName in new string[] { "VegaDesktopWidget.HWiNFORestartHelper.exe", "VegaDesktopWidget.FanHelper.exe", "VegaDesktopWidget.OpenHardwareMonitorLib.dll" })
        {
            using (Stream embedded = widgetAssembly.GetManifestResourceStream(resourceName))
                if (embedded == null || embedded.Length < 4096 || embedded.ReadByte() != 'M' || embedded.ReadByte() != 'Z') throw new Exception("Embedded runtime file is missing or invalid: " + resourceName);
        }
        Console.WriteLine("EmbeddedRuntimeFiles=PASS");
        string zip = Path.GetFullPath(args[1]);
        string root = Path.Combine(Path.GetFullPath(args[2]), "update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string stage = Path.Combine(root, "stage"), install = Path.Combine(root, "install");
        Directory.CreateDirectory(stage); Directory.CreateDirectory(install);
        Type type = Assembly.LoadFrom(executable).GetType("VegaDesktopWidget.UpdateService", true);
        MethodInfo extract = type.GetMethod("ExtractPackage", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo apply = type.GetMethod("InstallFiles", BindingFlags.Static | BindingFlags.NonPublic);
        string versioned = Path.GetFileName(executable);
        Invoke(extract, zip, stage, versioned);
        string[] files = { versioned, "OpenHardwareMonitor-License.html" };
        foreach (string name in files) if (!File.Exists(Path.Combine(stage, name))) throw new Exception("Missing extracted file: " + name);
        Console.WriteLine("ExtractedFiles=" + files.Length);
        string target = Path.Combine(install, "InstalledWidget.exe");
        File.WriteAllText(target, "old exe");
        foreach (string name in files) if (name != versioned) File.WriteAllText(Path.Combine(install, name), "old " + name);
        Invoke(apply, stage, target, versioned);
        if (Hash(target) != Hash(Path.Combine(stage, versioned))) throw new Exception("EXE replacement mismatch");
        foreach (string name in files) if (name != versioned && Hash(Path.Combine(install, name)) != Hash(Path.Combine(stage, name))) throw new Exception("Replacement mismatch: " + name);
        Console.WriteLine("Replacement=PASS");
        File.WriteAllText(target, "read-only old exe");
        foreach (string name in files) if (name != versioned) File.WriteAllText(Path.Combine(install, name), "old " + name);
        File.SetAttributes(target, FileAttributes.ReadOnly);
        bool failed = false;
        try { Invoke(apply, stage, target, versioned); }
        catch (UnauthorizedAccessException) { failed = true; }
        finally { File.SetAttributes(target, FileAttributes.Normal); }
        if (!failed || File.ReadAllText(target) != "read-only old exe") throw new Exception("Rollback did not preserve EXE");
        foreach (string name in files) if (name != versioned && File.ReadAllText(Path.Combine(install, name)) != "old " + name) throw new Exception("Rollback mismatch: " + name);
        Console.WriteLine("Rollback=PASS");
        Console.WriteLine("TestRoot=" + root);
    }
}
