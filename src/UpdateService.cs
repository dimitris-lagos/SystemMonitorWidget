using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal static class UpdateService
    {
        private const string LatestApi = "https://api.github.com/repos/dimitris-lagos/SystemMonitorWidget/releases/latest";
        private static readonly string[] PackageFiles = { "OpenHardwareMonitor-License.html" };
        private static int checking;

        [DataContract]
        private sealed class ReleaseInfo
        {
            [DataMember(Name = "tag_name")] public string tag_name { get; set; }
            [DataMember(Name = "assets")] public ReleaseAsset[] assets { get; set; }
        }

        [DataContract]
        private sealed class ReleaseAsset
        {
            [DataMember(Name = "name")] public string name { get; set; }
            [DataMember(Name = "browser_download_url")] public string browser_download_url { get; set; }
        }

        private sealed class TimedWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                request.Timeout = 12000;
                HttpWebRequest http = request as HttpWebRequest;
                if (http != null) { http.ReadWriteTimeout = 12000; http.UserAgent = "SystemMonitorWidget-Updater"; }
                return request;
            }
        }

        public static void CheckForUpdates(WidgetForm owner, bool manual)
        {
            if (Interlocked.Exchange(ref checking, 1) != 0) return;
            if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 0)
            {
                Interlocked.Exchange(ref checking, 0);
                if (manual) OnUi(owner, delegate { MessageBox.Show(owner, "Windows Vista cannot make the TLS 1.2 connection required by GitHub. Download updates manually from:\nhttps://github.com/dimitris-lagos/SystemMonitorWidget/releases/latest", "System Monitor updates", MessageBoxButtons.OK, MessageBoxIcon.Information); });
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ReleaseInfo release = FetchLatest();
                    Version latest;
                    Version current = Assembly.GetExecutingAssembly().GetName().Version;
                    if (!TryReleaseVersion(release.tag_name, out latest)) throw new InvalidDataException("The latest release has an invalid version tag.");
                    if (latest <= current)
                    {
                        if (manual) OnUi(owner, delegate { MessageBox.Show(owner, "You already have the latest version (" + current.ToString(3) + ").", "System Monitor updates", MessageBoxButtons.OK, MessageBoxIcon.Information); });
                        return;
                    }
                    string zipName = "SystemMonitorWidget-" + release.tag_name + "-win-x64.zip";
                    ReleaseAsset zip = FindAsset(release, zipName);
                    ReleaseAsset checksum = FindAsset(release, "SystemMonitorWidget-" + release.tag_name + "-win-x64.sha256.txt");
                    if (zip == null || checksum == null) throw new InvalidDataException("The release is missing its ZIP or SHA-256 asset.");
                    OnUi(owner, delegate
                    {
                        string message = "System Monitor " + latest.ToString(3) + " is available.\n\nDownload the verified release, replace the files in this folder, and restart the widget? Your settings will be kept. The EXE filename will stay the same.";
                        if (MessageBox.Show(owner, message, "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                        ThreadPool.QueueUserWorkItem(delegate { DownloadAndInstall(owner, release.tag_name, latest, zip, checksum); });
                    });
                }
                catch (Exception ex)
                {
                    if (manual) OnUi(owner, delegate { MessageBox.Show(owner, "Update check failed: " + ex.Message, "System Monitor updates", MessageBoxButtons.OK, MessageBoxIcon.Warning); });
                }
                finally { Interlocked.Exchange(ref checking, 0); }
            });
        }

        private static void DownloadAndInstall(WidgetForm owner, string tag, Version latest, ReleaseAsset zipAsset, ReleaseAsset checksumAsset)
        {
            try
            {
                string target = Application.ExecutablePath;
                string folder = Path.GetDirectoryName(target);
                bool elevate = !CanWrite(folder);
                string stage = Path.Combine(Path.GetTempPath(), "SystemMonitorWidget-update-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                string zip = Path.Combine(stage, "release.zip");
                SetTls12();
                using (TimedWebClient client = new TimedWebClient())
                {
                    string expected = client.DownloadString(CheckedUrl(checksumAsset.browser_download_url, tag, checksumAsset.name)).Trim();
                    if (expected.Length < 64 || !IsSha256(expected.Substring(0, 64)) || expected.IndexOf(zipAsset.name, StringComparison.Ordinal) < 0)
                        throw new InvalidDataException("The release checksum is malformed.");
                    client.DownloadFile(CheckedUrl(zipAsset.browser_download_url, tag, zipAsset.name), zip);
                    if (new FileInfo(zip).Length > 25000000) throw new InvalidDataException("The update package is unexpectedly large.");
                    using (SHA256 sha = SHA256.Create())
                    using (FileStream input = File.OpenRead(zip))
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
                        if (!actual.Equals(expected.Substring(0, 64), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The release checksum does not match.");
                    }
                }
                string exeName = "SystemMonitorWidget-" + tag + ".exe";
                ExtractPackage(zip, stage, exeName);
                Version packageVersion = AssemblyName.GetAssemblyName(Path.Combine(stage, exeName)).Version;
                if (packageVersion.Major != latest.Major || packageVersion.Minor != latest.Minor || packageVersion.Build != latest.Build)
                    throw new InvalidDataException("The EXE version does not match the release.");
                string updater = Path.Combine(stage, "SystemMonitorWidget.UpdateRunner.exe");
                File.Copy(target, updater);
                OnUi(owner, delegate
                {
                    try
                    {
                        ProcessStartInfo start = new ProcessStartInfo(updater);
                        start.Arguments = "--apply-update " + Process.GetCurrentProcess().Id + " " + Quote(target) + " " + Quote(stage) + " " + Quote(exeName);
                        start.WorkingDirectory = stage;
                        start.UseShellExecute = true;
                        if (elevate) start.Verb = "runas";
                        Process process = Process.Start(start);
                        if (process == null) throw new InvalidOperationException("Could not launch the updater.");
                        process.Dispose();
                        owner.Close();
                    }
                    catch (Exception ex) { MessageBox.Show(owner, "The update was downloaded but could not start: " + ex.Message, "System Monitor updates", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                });
            }
            catch (Exception ex) { OnUi(owner, delegate { MessageBox.Show(owner, "Update failed; the current installation was not changed.\n\n" + ex.Message, "System Monitor updates", MessageBoxButtons.OK, MessageBoxIcon.Warning); }); }
        }

        private static ReleaseInfo FetchLatest()
        {
            SetTls12();
            using (TimedWebClient client = new TimedWebClient())
            {
                client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                string json = client.DownloadString(LatestApi);
                ReleaseInfo value;
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    value = (ReleaseInfo)new DataContractJsonSerializer(typeof(ReleaseInfo)).ReadObject(stream);
                }
                if (value == null || value.assets == null) throw new InvalidDataException("The release response is incomplete.");
                return value;
            }
        }

        private static void SetTls12() { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }

        private static bool TryReleaseVersion(string tag, out Version version)
        {
            version = null;
            if (String.IsNullOrEmpty(tag) || tag[0] != 'v') return false;
            return Version.TryParse(tag.Substring(1), out version) && version.Build >= 0 && version.Revision < 0;
        }

        private static ReleaseAsset FindAsset(ReleaseInfo release, string name)
        {
            foreach (ReleaseAsset asset in release.assets)
                if (asset != null && asset.name == name) return asset;
            return null;
        }

        private static Uri CheckedUrl(string address, string tag, string name)
        {
            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com"
                || uri.AbsolutePath != "/dimitris-lagos/SystemMonitorWidget/releases/download/" + tag + "/" + name)
                throw new InvalidDataException("Unexpected release asset URL.");
            return uri;
        }

        private static string Quote(string path) { return "\"" + path.Replace("\"", "") + "\""; }

        private static bool CanWrite(string directory)
        {
            try
            {
                string probe = Path.Combine(directory, ".systemmonitor-update-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException) { return false; }
        }

        private static void OnUi(Control owner, Action action)
        {
            try { if (!owner.IsDisposed && owner.IsHandleCreated) owner.BeginInvoke(action); }
            catch (InvalidOperationException) { }
        }

        public static bool IsInstallerCommand(string[] args) { return args.Length > 0 && args[0] == "--apply-update"; }

        public static void RunInstaller(string[] args)
        {
            if (args.Length != 5) return;
            int pid;
            if (!Int32.TryParse(args[1], out pid) || pid <= 0) return;
            string target = Path.GetFullPath(args[2]);
            string stage = Path.GetFullPath(args[3]);
            string exeName = args[4];
            if (Path.GetFileName(exeName) != exeName || !exeName.StartsWith("SystemMonitorWidget-v", StringComparison.Ordinal) || !exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                try { using (Process previous = Process.GetProcessById(pid)) { if (!previous.WaitForExit(30000)) throw new IOException("The widget did not close in time."); } }
                catch (ArgumentException) { }
                InstallFiles(stage, target, exeName);
                ProcessStartInfo restart = new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) };
                using (Process process = Process.Start(restart)) { }
            }
            catch (Exception ex) { MessageBox.Show("Update installation failed. The previous files were restored where possible.\n\n" + ex.Message, "System Monitor updates", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        internal static void InstallFiles(string stage, string targetExe, string exeName)
        {
            string directory = Path.GetDirectoryName(targetExe);
            string[] sources = new string[PackageFiles.Length + 1], targets = new string[PackageFiles.Length + 1];
            Array.Copy(PackageFiles, sources, PackageFiles.Length); Array.Copy(PackageFiles, targets, PackageFiles.Length);
            sources[PackageFiles.Length] = exeName; targets[PackageFiles.Length] = Path.GetFileName(targetExe);
            string backup = Path.Combine(stage, "backup");
            Directory.CreateDirectory(backup);
            List<int> touched = new List<int>();
            try
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    string source = Path.Combine(stage, sources[i]);
                    string destination = Path.Combine(directory, targets[i]);
                    if (!File.Exists(source)) throw new FileNotFoundException("A release file is missing.", source);
                    if (File.Exists(destination)) File.Copy(destination, Path.Combine(backup, targets[i]), true);
                    touched.Add(i);
                    CopyWithRetry(source, destination);
                }
            }
            catch
            {
                for (int j = touched.Count - 1; j >= 0; j--)
                {
                    int i = touched[j];
                    string original = Path.Combine(backup, targets[i]);
                    string destination = Path.Combine(directory, targets[i]);
                    try { if (File.Exists(original)) CopyWithRetry(original, destination); else if (File.Exists(destination)) File.Delete(destination); }
                    catch { }
                }
                throw;
            }
        }

        private static void CopyWithRetry(string source, string destination)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { File.Copy(source, destination, true); return; }
                catch (IOException) { if (attempt >= 19) throw; Thread.Sleep(500); }
            }
        }

        private static bool IsSha256(string text)
        {
            if (text.Length != 64) return false;
            foreach (char c in text) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        internal static void ExtractPackage(string zip, string destination, string exeName)
        {
            HashSet<string> required = new HashSet<string>(PackageFiles, StringComparer.Ordinal);
            required.Add(exeName);
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            using (FileStream stream = File.OpenRead(zip))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                long start = Math.Max(0, stream.Length - 65557);
                long eocd = -1;
                for (long p = stream.Length - 22; p >= start; p--) { stream.Position = p; if (reader.ReadUInt32() == 0x06054b50) { eocd = p; break; } }
                if (eocd < 0) throw new InvalidDataException("ZIP directory not found.");
                stream.Position = eocd + 10;
                int count = reader.ReadUInt16();
                reader.ReadUInt32();
                long central = reader.ReadUInt32();
                if (count > 30 || central >= stream.Length) throw new InvalidDataException("Invalid ZIP directory.");
                stream.Position = central;
                for (int i = 0; i < count; i++)
                {
                    if (reader.ReadUInt32() != 0x02014b50) throw new InvalidDataException("Invalid ZIP entry.");
                    reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt16();
                    int method = reader.ReadUInt16();
                    reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32();
                    long packed = reader.ReadUInt32(), unpacked = reader.ReadUInt32();
                    int nameLength = reader.ReadUInt16(), extraLength = reader.ReadUInt16(), commentLength = reader.ReadUInt16();
                    reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32();
                    long local = reader.ReadUInt32();
                    string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                    stream.Position += extraLength + commentLength;
                    long next = stream.Position;
                    if (!required.Contains(name)) { stream.Position = next; continue; }
                    if (!found.Add(name) || packed > 10000000 || unpacked > 10000000 || (method != 0 && method != 8))
                        throw new InvalidDataException("Invalid release file in ZIP.");
                    stream.Position = local;
                    if (reader.ReadUInt32() != 0x04034b50) throw new InvalidDataException("Invalid ZIP local entry.");
                    reader.ReadBytes(22);
                    int localName = reader.ReadUInt16(), localExtra = reader.ReadUInt16();
                    stream.Position += localName + localExtra;
                    byte[] compressed = reader.ReadBytes((int)packed);
                    if (compressed.Length != packed) throw new InvalidDataException("Incomplete ZIP entry.");
                    byte[] content;
                    if (method == 0) content = compressed;
                    else using (MemoryStream input = new MemoryStream(compressed))
                    using (DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress))
                    using (MemoryStream output = new MemoryStream())
                    { inflate.CopyTo(output); content = output.ToArray(); }
                    if (content.Length != unpacked) throw new InvalidDataException("ZIP entry size mismatch.");
                    File.WriteAllBytes(Path.Combine(destination, name), content);
                    stream.Position = next;
                }
            }
            if (found.Count != required.Count) throw new InvalidDataException("The release ZIP is missing required files.");
        }
    }
}
