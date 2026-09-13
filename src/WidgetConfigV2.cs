using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace VegaDesktopWidget
{
    internal sealed class WidgetConfig
    {
        public static string DefaultHeaderTitle { get { Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; return "SYSTEM MONITOR v" + version.Major + "." + version.Minor; } }
        public int Left = 60, Top = 60, Width = 370, UiScaleMode = 100, GridColumns = 4, RefreshMilliseconds = 1000, OpacityPercent = 96;
        public int ProcessStripMode = 2;
        public string HeaderTitle = DefaultHeaderTitle;
        public string HWiNFOExecutablePath = "";
        public int CpuGraphMin = 0, CpuGraphMax = 150, GpuGraphMin = 0, GpuGraphMax = 350;
        public bool AlwaysOnTop = false, LockPosition = false, ShowGraphs = true, LaunchHWiNFO = false, AutoRestartHWiNFO = false;
        public bool FanControlEnabled = false;
        public bool SystemNetworkDefaultsAdded = false;
        public bool CompactNetworkGraphsAdded = false;
        public bool CompactNetworkExtremaAdded = false;
        public bool RamColorsEverywhereAdded = false;
        public bool CpuSectionNameInitialized = false, GpuSectionNameInitialized = false, NetworkSectionNameInitialized = false;
        public int[] CustomColors = new int[0];
        public List<FanProfile> FanProfiles = new List<FanProfile>();
        public Dictionary<string, string> RoleKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RoleLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<DashboardItem> Dashboard3 = new List<DashboardItem>(), Dashboard4 = new List<DashboardItem>();
        public int DashboardRows3 = DashboardDefaults.Rows, DashboardRows4 = DashboardDefaults.Rows;
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VegaDesktopWidget"); } }
        public static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }

        public string Label(string key, string fallback)
        {
            string value; return RoleLabels.TryGetValue(key, out value) && !String.IsNullOrWhiteSpace(value) ? value.Trim().ToUpperInvariant() : fallback;
        }

        public static WidgetConfig Load()
        {
            WidgetConfig c = new WidgetConfig();
            if (!File.Exists(FilePath))
            {
                c.Dashboard3 = DashboardDefaults.Create(3); c.Dashboard4 = DashboardDefaults.Create(4); c.SystemNetworkDefaultsAdded = true; c.CompactNetworkGraphsAdded = true; c.CompactNetworkExtremaAdded = true; c.RamColorsEverywhereAdded = true; return c;
            }
            foreach (string raw in File.ReadAllLines(FilePath))
            {
                string line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('='); if (eq <= 0) continue; string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim(); int n; bool f;
                if (k.Equals("Left", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.Left = n;
                else if (k.Equals("Top", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.Top = n;
                else if (k.Equals("Width", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.Width = Math.Max(340, Math.Min(600, n));
                else if (k.Equals("GridColumns", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n) && (n == 3 || n == 4)) c.GridColumns = n;
                else if (k.Equals("UiScaleDivisor", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.UiScaleMode = n == 1 ? 100 : n == 2 ? 50 : n == 3 ? 33 : 25;
                else if (k.Equals("UiScaleMode", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n) && IsUiScaleMode(n)) c.UiScaleMode = n;
                else if (k.Equals("RefreshMilliseconds", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.RefreshMilliseconds = Math.Max(500, Math.Min(5000, n));
                else if (k.Equals("OpacityPercent", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.OpacityPercent = Math.Max(65, Math.Min(100, n));
                else if (k.Equals("ProcessStripMode", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n) && n >= 0 && n <= 2) c.ProcessStripMode = n;
                else if (k.Equals("HeaderTitle", StringComparison.OrdinalIgnoreCase)) { string title = NormalizeHeaderTitle(v); c.HeaderTitle = IsVersionedDefaultHeaderTitle(title) ? DefaultHeaderTitle : title; }
                else if (k.Equals("CpuGraphMin", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.CpuGraphMin = Math.Max(0, n);
                else if (k.Equals("CpuGraphMax", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.CpuGraphMax = Math.Max(1, n);
                else if (k.Equals("GpuGraphMin", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.GpuGraphMin = Math.Max(0, n);
                else if (k.Equals("GpuGraphMax", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.GpuGraphMax = Math.Max(1, n);
                else if (k.Equals("AlwaysOnTop", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.AlwaysOnTop = f;
                else if (k.Equals("LockPosition", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.LockPosition = f;
                else if (k.Equals("ShowGraphs", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.ShowGraphs = f;
                else if (k.Equals("LaunchHWiNFO", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.LaunchHWiNFO = f;
                else if (k.Equals("AutoRestartHWiNFO", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.AutoRestartHWiNFO = f;
                else if (k.Equals("HWiNFOExecutablePath", StringComparison.OrdinalIgnoreCase)) c.HWiNFOExecutablePath = NormalizeHWiNFOExecutablePath(v);
                else if (k.Equals("FanControlEnabled", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.FanControlEnabled = f;
                else if (k.Equals("SystemNetworkDefaultsAdded", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.SystemNetworkDefaultsAdded = f;
                else if (k.Equals("CompactNetworkGraphsAdded", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.CompactNetworkGraphsAdded = f;
                else if (k.Equals("CompactNetworkExtremaAdded", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.CompactNetworkExtremaAdded = f;
                else if (k.Equals("RamColorsEverywhereAdded", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.RamColorsEverywhereAdded = f;
                else if (k.Equals("CpuSectionNameInitialized", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.CpuSectionNameInitialized = f;
                else if (k.Equals("GpuSectionNameInitialized", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.GpuSectionNameInitialized = f;
                else if (k.Equals("NetworkSectionNameInitialized", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.NetworkSectionNameInitialized = f;
                else if (k.Equals("CustomColors", StringComparison.OrdinalIgnoreCase)) c.CustomColors = ParseCustomColors(v);
                else if (k.StartsWith("FanProfile.", StringComparison.OrdinalIgnoreCase)) { FanProfile profile = FanProfile.Deserialize(v); if (profile != null) c.FanProfiles.Add(profile); }
                else if (k.Equals("DashboardRows3", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.DashboardRows3 = Math.Max(4, Math.Min(30, n));
                else if (k.Equals("DashboardRows4", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.DashboardRows4 = Math.Max(4, Math.Min(30, n));
                else if (k.StartsWith("Item3.", StringComparison.OrdinalIgnoreCase)) { DashboardItem item = DashboardItem.Deserialize(v); if (item != null) c.Dashboard3.Add(item); }
                else if (k.StartsWith("Item4.", StringComparison.OrdinalIgnoreCase)) { DashboardItem item = DashboardItem.Deserialize(v); if (item != null) c.Dashboard4.Add(item); }
                else if (k.StartsWith("Role.", StringComparison.OrdinalIgnoreCase)) c.RoleKeys[k.Substring(5)] = v;
                else if (k.StartsWith("Label.", StringComparison.OrdinalIgnoreCase)) c.RoleLabels[k.Substring(6)] = v;
            }
            if (c.CpuGraphMax <= c.CpuGraphMin) c.CpuGraphMax = c.CpuGraphMin + 10;
            if (c.GpuGraphMax <= c.GpuGraphMin) c.GpuGraphMax = c.GpuGraphMin + 10;
            if (c.Dashboard3.Count == 0) { c.Dashboard3 = DashboardDefaults.Create(3); c.DashboardRows3 = Math.Max(c.DashboardRows3, DashboardDefaults.Rows); }
            if (c.Dashboard4.Count == 0) { c.Dashboard4 = DashboardDefaults.Create(4); c.DashboardRows4 = Math.Max(c.DashboardRows4, DashboardDefaults.Rows); }
            if (!c.SystemNetworkDefaultsAdded)
            {
                c.DashboardRows3 = DashboardDefaults.AddSystemNetworkDefaults(c.Dashboard3, 3, c.DashboardRows3);
                c.DashboardRows4 = DashboardDefaults.AddSystemNetworkDefaults(c.Dashboard4, 4, c.DashboardRows4);
                c.SystemNetworkDefaultsAdded = true;
            }
            if (!c.CompactNetworkGraphsAdded)
            {
                c.DashboardRows3 = DashboardDefaults.ConvertNetworkMetricsToCompactGraphs(c.Dashboard3, 3, c.DashboardRows3);
                c.DashboardRows4 = DashboardDefaults.ConvertNetworkMetricsToCompactGraphs(c.Dashboard4, 4, c.DashboardRows4);
                c.CompactNetworkGraphsAdded = true;
            }
            if (!c.CompactNetworkExtremaAdded)
            {
                foreach (DashboardItem item in c.Dashboard3) if (item.SensorKey.StartsWith("role:Network", StringComparison.OrdinalIgnoreCase)) item.ShowExtrema = true;
                foreach (DashboardItem item in c.Dashboard4) if (item.SensorKey.StartsWith("role:Network", StringComparison.OrdinalIgnoreCase)) item.ShowExtrema = true;
                c.CompactNetworkExtremaAdded = true;
            }
            if (!c.RamColorsEverywhereAdded)
            {
                foreach (DashboardItem item in c.Dashboard3) item.Colors = DashboardItem.AlertColors();
                foreach (DashboardItem item in c.Dashboard4) item.Colors = DashboardItem.AlertColors();
                c.RamColorsEverywhereAdded = true;
            }
            return c;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder); List<string> l = new List<string>(); l.Add("# System Monitor Widget modular settings v2.0");
            l.Add("Left=" + Left.ToString(CultureInfo.InvariantCulture)); l.Add("Top=" + Top.ToString(CultureInfo.InvariantCulture)); l.Add("Width=" + Width.ToString(CultureInfo.InvariantCulture)); l.Add("UiScaleMode=" + UiScaleMode.ToString(CultureInfo.InvariantCulture)); l.Add("GridColumns=" + GridColumns.ToString(CultureInfo.InvariantCulture));
            l.Add("RefreshMilliseconds=" + RefreshMilliseconds.ToString(CultureInfo.InvariantCulture)); l.Add("OpacityPercent=" + OpacityPercent.ToString(CultureInfo.InvariantCulture));
            l.Add("ProcessStripMode=" + ProcessStripMode.ToString(CultureInfo.InvariantCulture));
            l.Add("HeaderTitle=" + NormalizeHeaderTitle(HeaderTitle));
            l.Add("CpuGraphMin=" + CpuGraphMin); l.Add("CpuGraphMax=" + CpuGraphMax); l.Add("GpuGraphMin=" + GpuGraphMin); l.Add("GpuGraphMax=" + GpuGraphMax);
            l.Add("AlwaysOnTop=" + AlwaysOnTop); l.Add("LockPosition=" + LockPosition); l.Add("ShowGraphs=" + ShowGraphs); l.Add("LaunchHWiNFO=" + LaunchHWiNFO); l.Add("AutoRestartHWiNFO=" + AutoRestartHWiNFO);
            l.Add("HWiNFOExecutablePath=" + NormalizeHWiNFOExecutablePath(HWiNFOExecutablePath));
            l.Add("FanControlEnabled=" + FanControlEnabled);
            l.Add("SystemNetworkDefaultsAdded=" + SystemNetworkDefaultsAdded);
            l.Add("CompactNetworkGraphsAdded=" + CompactNetworkGraphsAdded);
            l.Add("CompactNetworkExtremaAdded=" + CompactNetworkExtremaAdded);
            l.Add("RamColorsEverywhereAdded=" + RamColorsEverywhereAdded);
            l.Add("CpuSectionNameInitialized=" + CpuSectionNameInitialized); l.Add("GpuSectionNameInitialized=" + GpuSectionNameInitialized); l.Add("NetworkSectionNameInitialized=" + NetworkSectionNameInitialized);
            l.Add("CustomColors=" + String.Join(",", Array.ConvertAll(CustomColors ?? new int[0], delegate(int color) { return color.ToString(CultureInfo.InvariantCulture); })));
            for (int i = 0; i < FanProfiles.Count; i++) l.Add("FanProfile." + i.ToString("D3", CultureInfo.InvariantCulture) + "=" + FanProfiles[i].Serialize());
            l.Add("DashboardRows3=" + DashboardRows3); l.Add("DashboardRows4=" + DashboardRows4);
            for (int i = 0; i < Dashboard3.Count; i++) l.Add("Item3." + i.ToString("D3", CultureInfo.InvariantCulture) + "=" + Dashboard3[i].Serialize());
            for (int i = 0; i < Dashboard4.Count; i++) l.Add("Item4." + i.ToString("D3", CultureInfo.InvariantCulture) + "=" + Dashboard4[i].Serialize());
            foreach (KeyValuePair<string, string> p in RoleKeys) l.Add("Role." + p.Key + "=" + p.Value);
            foreach (KeyValuePair<string, string> p in RoleLabels) l.Add("Label." + p.Key + "=" + p.Value.Replace("\r", " ").Replace("\n", " "));
            File.WriteAllLines(FilePath, l.ToArray());
        }
        public List<DashboardItem> ActiveDashboard { get { return GridColumns == 3 ? Dashboard3 : Dashboard4; } }
        public int ActiveDashboardRows { get { return GridColumns == 3 ? DashboardRows3 : DashboardRows4; } }
        public static bool IsUiScaleMode(int value) { return value == 100 || value == 95 || value == 90 || value == 85 || value == 80 || value == 75 || value == 67 || value == 50 || value == 33 || value == 25; }
        public static float UiScaleFactor(int value) { if (value == 67) return 2f / 3f; if (value == 33) return 1f / 3f; return IsUiScaleMode(value) ? value / 100f : 1f; }
        private static int[] ParseCustomColors(string value)
        {
            List<int> colors = new List<int>();
            foreach (string part in (value ?? "").Split(','))
            {
                int color; if (Int32.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out color) && color >= 0 && color <= 0xFFFFFF) colors.Add(color);
                if (colors.Count == 16) break;
            }
            return colors.ToArray();
        }
        public static string NormalizeHeaderTitle(string value) { string title = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim(); if (title.Length == 0) return DefaultHeaderTitle; return title.Length > 48 ? title.Substring(0, 48) : title; }
        public static string NormalizeHWiNFOExecutablePath(string value)
        {
            string path = (value ?? "").Trim();
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"') path = path.Substring(1, path.Length - 2).Trim();
            if (path.Length == 0) return "";
            try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)); }
            catch { return path; }
        }
        public static bool IsHWiNFOExecutablePath(string value)
        {
            try
            {
                string path = NormalizeHWiNFOExecutablePath(value), name = Path.GetFileName(path);
                return File.Exists(path) && (String.Equals(name, "HWiNFO64.exe", StringComparison.OrdinalIgnoreCase) || String.Equals(name, "HWiNFO32.exe", StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }
        public string ResolveHWiNFOExecutablePath()
        {
            string configured = NormalizeHWiNFOExecutablePath(HWiNFOExecutablePath);
            if (IsHWiNFOExecutablePath(configured)) return configured;
            string[] names = Environment.OSVersion.Version.Major <= 6 ? new string[] { "HWiNFO32.exe", "HWiNFO64.exe" } : new string[] { "HWiNFO64.exe", "HWiNFO32.exe" };
            string appFolder = Path.GetDirectoryName(typeof(WidgetConfig).Assembly.Location) ?? "";
            foreach (string name in names) { string candidate = NormalizeHWiNFOExecutablePath(Path.Combine(appFolder, name)); if (IsHWiNFOExecutablePath(candidate)) return candidate; }
            string running = FindRunningHWiNFOExecutable(); if (running.Length > 0) return running;
            string[] roots = new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            foreach (string root in roots)
                foreach (string name in names)
                {
                    string candidate = NormalizeHWiNFOExecutablePath(Path.Combine(root, Path.GetFileNameWithoutExtension(name), name));
                    if (IsHWiNFOExecutablePath(candidate)) return candidate;
                }
            return "";
        }
        private static string FindRunningHWiNFOExecutable()
        {
            foreach (string name in new string[] { "HWiNFO64", "HWiNFO32" })
            {
                Process[] processes = new Process[0];
                try
                {
                    processes = Process.GetProcessesByName(name);
                    foreach (Process process in processes) { try { string path = NormalizeHWiNFOExecutablePath(process.MainModule.FileName); if (IsHWiNFOExecutablePath(path)) return path; } catch { } }
                }
                catch { }
                finally { foreach (Process process in processes) process.Dispose(); }
            }
            return "";
        }
        private static bool IsVersionedDefaultHeaderTitle(string value) { Version version; const string prefix = "SYSTEM MONITOR v"; return value != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Version.TryParse(value.Substring(prefix.Length), out version); }
        public void SetDashboardRows(int columns, int rows) { if (columns == 3) DashboardRows3 = rows; else DashboardRows4 = rows; }
        public static bool IsStartupEnabled() { using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false)) return k != null && k.GetValue("VegaDesktopWidget") != null; }
        public static void SetStartup(bool enabled) { using (RegistryKey k = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")) { if (enabled) k.SetValue("VegaDesktopWidget", "\"" + System.Windows.Forms.Application.ExecutablePath + "\""); else k.DeleteValue("VegaDesktopWidget", false); } }
    }
}
