using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace VegaDesktopWidget
{
    internal static class EmbeddedSupportFiles
    {
        private const string FanHelperResource = "VegaDesktopWidget.FanHelper.exe";
        private const string HWiNFORestartHelperResource = "VegaDesktopWidget.HWiNFORestartHelper.exe";
        private const string OpenHardwareMonitorResource = "VegaDesktopWidget.OpenHardwareMonitorLib.dll";
        private static readonly object sync = new object();

        public static string FanHelperPath()
        {
            lock (sync)
            {
                EnsureRuntimeFile(OpenHardwareMonitorResource, "OpenHardwareMonitorLib.dll");
                return EnsureRuntimeFile(FanHelperResource, "SystemMonitorWidget.FanHelper.exe");
            }
        }

        public static string HWiNFORestartHelperPath()
        {
            lock (sync) return EnsureRuntimeFile(HWiNFORestartHelperResource, "SystemMonitorWidget.HWiNFORestartHelper.exe");
        }

        public static void PrepareAndCleanLegacyInstallFiles()
        {
            FanHelperPath(); HWiNFORestartHelperPath();
            string install = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string name in new string[] { "SystemMonitorWidget.FanHelper.exe", "SystemMonitorWidget.HWiNFORestartHelper.exe", "OpenHardwareMonitorLib.dll", "OpenHardwareMonitorLib.sys" })
            {
                try { string path = Path.Combine(install, name); if (File.Exists(path)) File.Delete(path); }
                catch { }
            }
        }

        private static string EnsureRuntimeFile(string resourceName, string fileName)
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string directory = Path.Combine(WidgetConfig.Folder, "Runtime", version.ToString()); Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, fileName); byte[] content = ReadResource(resourceName);
            if (File.Exists(destination) && SameContent(destination, content)) return destination;
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, content); File.Copy(temporary, destination, true); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
            if (!SameContent(destination, content)) throw new IOException("Could not verify extracted runtime file: " + fileName);
            return destination;
        }

        private static byte[] ReadResource(string resourceName)
        {
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new FileNotFoundException("Embedded runtime resource is missing: " + resourceName);
                using (MemoryStream output = new MemoryStream()) { input.CopyTo(output); return output.ToArray(); }
            }
        }

        private static bool SameContent(string path, byte[] expected)
        {
            try
            {
                FileInfo info = new FileInfo(path); if (info.Length != expected.Length) return false;
                using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path))
                {
                    byte[] actualHash = sha.ComputeHash(stream), expectedHash = sha.ComputeHash(expected);
                    if (actualHash.Length != expectedHash.Length) return false;
                    for (int i = 0; i < actualHash.Length; i++) if (actualHash[i] != expectedHash[i]) return false;
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
