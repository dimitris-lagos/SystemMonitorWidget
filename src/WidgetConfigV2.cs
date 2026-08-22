using System;
using System.Collections.Generic;
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
        public int CpuGraphMin = 0, CpuGraphMax = 150, GpuGraphMin = 0, GpuGraphMax = 350;
        public bool AlwaysOnTop = false, ShowGraphs = true, LaunchHWiNFO = false;
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
            WidgetConfig c = new WidgetConfig(); if (!File.Exists(FilePath)) return c;
            foreach (string raw in File.ReadAllLines(FilePath))
            {
                string line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('='); if (eq <= 0) continue; string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim(); int n; bool f;
                if (k.Equals("Left", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.Left = n;
                else if (k.Equals("Top", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.Top = n;
                else if (k.Equals("Width", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.Width = Math.Max(340, Math.Min(600, n));
                else if (k.Equals("GridColumns", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n) && (n == 3 || n == 4)) c.GridColumns = n;
                else if (k.Equals("UiScaleDivisor", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.UiScaleMode = n == 1 ? 100 : n == 2 ? 50 : n == 3 ? 33 : 25;
                else if (k.Equals("UiScaleMode", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n) && (n == 100 || n == 75 || n == 67 || n == 50 || n == 33 || n == 25)) c.UiScaleMode = n;
                else if (k.Equals("RefreshMilliseconds", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.RefreshMilliseconds = Math.Max(500, Math.Min(5000, n));
                else if (k.Equals("OpacityPercent", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.OpacityPercent = Math.Max(65, Math.Min(100, n));
                else if (k.Equals("ProcessStripMode", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n) && n >= 0 && n <= 2) c.ProcessStripMode = n;
                else if (k.Equals("HeaderTitle", StringComparison.OrdinalIgnoreCase)) { string title = NormalizeHeaderTitle(v); c.HeaderTitle = IsVersionedDefaultHeaderTitle(title) ? DefaultHeaderTitle : title; }
                else if (k.Equals("CpuGraphMin", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.CpuGraphMin = Math.Max(0, n);
                else if (k.Equals("CpuGraphMax", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.CpuGraphMax = Math.Max(1, n);
                else if (k.Equals("GpuGraphMin", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.GpuGraphMin = Math.Max(0, n);
                else if (k.Equals("GpuGraphMax", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.GpuGraphMax = Math.Max(1, n);
                else if (k.Equals("AlwaysOnTop", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.AlwaysOnTop = f;
                else if (k.Equals("ShowGraphs", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.ShowGraphs = f;
                else if (k.Equals("LaunchHWiNFO", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(v, out f)) c.LaunchHWiNFO = f;
                else if (k.Equals("DashboardRows3", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.DashboardRows3 = Math.Max(4, Math.Min(30, n));
                else if (k.Equals("DashboardRows4", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(v, out n)) c.DashboardRows4 = Math.Max(4, Math.Min(30, n));
                else if (k.StartsWith("Item3.", StringComparison.OrdinalIgnoreCase)) { DashboardItem item = DashboardItem.Deserialize(v); if (item != null) c.Dashboard3.Add(item); }
                else if (k.StartsWith("Item4.", StringComparison.OrdinalIgnoreCase)) { DashboardItem item = DashboardItem.Deserialize(v); if (item != null) c.Dashboard4.Add(item); }
                else if (k.StartsWith("Role.", StringComparison.OrdinalIgnoreCase)) c.RoleKeys[k.Substring(5)] = v;
                else if (k.StartsWith("Label.", StringComparison.OrdinalIgnoreCase)) c.RoleLabels[k.Substring(6)] = v;
            }
            if (c.CpuGraphMax <= c.CpuGraphMin) c.CpuGraphMax = c.CpuGraphMin + 10;
            if (c.GpuGraphMax <= c.GpuGraphMin) c.GpuGraphMax = c.GpuGraphMin + 10;
            if (c.Dashboard3.Count == 0) c.Dashboard3 = DashboardDefaults.Create(3);
            if (c.Dashboard4.Count == 0) c.Dashboard4 = DashboardDefaults.Create(4);
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
            l.Add("AlwaysOnTop=" + AlwaysOnTop); l.Add("ShowGraphs=" + ShowGraphs); l.Add("LaunchHWiNFO=" + LaunchHWiNFO);
            l.Add("DashboardRows3=" + DashboardRows3); l.Add("DashboardRows4=" + DashboardRows4);
            for (int i = 0; i < Dashboard3.Count; i++) l.Add("Item3." + i.ToString("D3", CultureInfo.InvariantCulture) + "=" + Dashboard3[i].Serialize());
            for (int i = 0; i < Dashboard4.Count; i++) l.Add("Item4." + i.ToString("D3", CultureInfo.InvariantCulture) + "=" + Dashboard4[i].Serialize());
            foreach (KeyValuePair<string, string> p in RoleKeys) l.Add("Role." + p.Key + "=" + p.Value);
            foreach (KeyValuePair<string, string> p in RoleLabels) l.Add("Label." + p.Key + "=" + p.Value.Replace("\r", " ").Replace("\n", " "));
            File.WriteAllLines(FilePath, l.ToArray());
        }
        public List<DashboardItem> ActiveDashboard { get { return GridColumns == 3 ? Dashboard3 : Dashboard4; } }
        public int ActiveDashboardRows { get { return GridColumns == 3 ? DashboardRows3 : DashboardRows4; } }
        public static string NormalizeHeaderTitle(string value) { string title = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim(); if (title.Length == 0) return DefaultHeaderTitle; return title.Length > 48 ? title.Substring(0, 48) : title; }
        private static bool IsVersionedDefaultHeaderTitle(string value) { Version version; const string prefix = "SYSTEM MONITOR v"; return value != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Version.TryParse(value.Substring(prefix.Length), out version); }
        public void SetDashboardRows(int columns, int rows) { if (columns == 3) DashboardRows3 = rows; else DashboardRows4 = rows; }
        public static bool IsStartupEnabled() { using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false)) return k != null && k.GetValue("VegaDesktopWidget") != null; }
        public static void SetStartup(bool enabled) { using (RegistryKey k = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")) { if (enabled) k.SetValue("VegaDesktopWidget", "\"" + System.Windows.Forms.Application.ExecutablePath + "\""); else k.DeleteValue("VegaDesktopWidget", false); } }
    }
}
