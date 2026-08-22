using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace VegaDesktopWidget
{
    internal enum DashboardBoxType { Big, Horizontal, Vertical, Graph, Section }

    internal sealed class DashboardItem
    {
        public string Id = Guid.NewGuid().ToString("N");
        public DashboardBoxType BoxType = DashboardBoxType.Big;
        public string SensorKey = "";
        public string SensorLabel = "";
        public string SensorName = "";
        public string DisplayName = "NEW SENSOR";
        public int Column;
        public int Row;
        public int ColumnSpan = 1;
        public int RowSpan = 2;
        public bool ShowExtrema;
        public double GraphMinimum;
        public double GraphMaximum = 100;
        public double[] Thresholds = new double[] { 20, 40, 60, 80 };
        public int[] Colors = ActivityColors();

        public DashboardItem Clone()
        {
            DashboardItem copy = (DashboardItem)MemberwiseClone();
            copy.Thresholds = (double[])Thresholds.Clone();
            copy.Colors = (int[])Colors.Clone();
            return copy;
        }

        public void ApplyTypeDefaults(int columns)
        {
            if (BoxType == DashboardBoxType.Horizontal) { ColumnSpan = 1; RowSpan = 1; ShowExtrema = false; }
            else if (BoxType == DashboardBoxType.Graph) { ColumnSpan = columns; RowSpan = 2; ShowExtrema = false; }
            else if (BoxType == DashboardBoxType.Section) { ColumnSpan = columns; RowSpan = 1; ShowExtrema = false; SensorKey = ""; }
            else { ColumnSpan = 1; RowSpan = 2; }
            Column = Math.Max(0, Math.Min(columns - ColumnSpan, Column));
        }

        public Color ColorFor(double value)
        {
            int index = value < Thresholds[0] ? 0 : value < Thresholds[1] ? 1 : value < Thresholds[2] ? 2 : value < Thresholds[3] ? 3 : 4;
            return Color.FromArgb(Colors[Math.Max(0, Math.Min(4, index))]);
        }

        public string Serialize()
        {
            return String.Join("|", new string[] {
                Escape(Id), BoxType.ToString(), Escape(SensorKey), Escape(SensorLabel), Escape(SensorName), Escape(DisplayName),
                Column.ToString(CultureInfo.InvariantCulture), Row.ToString(CultureInfo.InvariantCulture), ColumnSpan.ToString(CultureInfo.InvariantCulture), RowSpan.ToString(CultureInfo.InvariantCulture),
                ShowExtrema.ToString(), GraphMinimum.ToString("R", CultureInfo.InvariantCulture), GraphMaximum.ToString("R", CultureInfo.InvariantCulture),
                JoinDoubles(Thresholds), JoinInts(Colors)
            });
        }

        public static DashboardItem Deserialize(string value)
        {
            string[] p = value.Split('|');
            if (p.Length < 15) return null;
            DashboardItem item = new DashboardItem();
            DashboardBoxType type; int n; double d; bool flag;
            item.Id = Unescape(p[0]);
            if (Enum.TryParse<DashboardBoxType>(p[1], true, out type)) item.BoxType = type;
            item.SensorKey = Unescape(p[2]); item.SensorLabel = Unescape(p[3]); item.SensorName = Unescape(p[4]); item.DisplayName = Unescape(p[5]);
            if (Int32.TryParse(p[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) item.Column = n;
            if (Int32.TryParse(p[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) item.Row = n;
            if (Int32.TryParse(p[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) item.ColumnSpan = n;
            if (Int32.TryParse(p[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) item.RowSpan = n;
            if (Boolean.TryParse(p[10], out flag)) item.ShowExtrema = flag;
            if (Double.TryParse(p[11], NumberStyles.Float, CultureInfo.InvariantCulture, out d)) item.GraphMinimum = d;
            if (Double.TryParse(p[12], NumberStyles.Float, CultureInfo.InvariantCulture, out d)) item.GraphMaximum = d;
            item.Thresholds = ParseDoubles(p[13], new double[] { 20, 40, 60, 80 });
            item.Colors = ParseInts(p[14], ActivityColors());
            if (item.Id.Length == 0) item.Id = Guid.NewGuid().ToString("N");
            return item;
        }

        public static int[] ActivityColors()
        {
            return new int[] {
                Color.FromArgb(94, 148, 210).ToArgb(), Color.FromArgb(73, 190, 230).ToArgb(),
                Color.FromArgb(91, 211, 166).ToArgb(), Color.FromArgb(194, 224, 92).ToArgb(),
                Color.FromArgb(198, 153, 255).ToArgb()
            };
        }

        public static int[] AlertColors()
        {
            return new int[] {
                Color.FromArgb(86, 214, 147).ToArgb(), Color.FromArgb(75, 194, 230).ToArgb(),
                Color.FromArgb(235, 211, 88).ToArgb(), Color.FromArgb(255, 157, 72).ToArgb(),
                Color.FromArgb(255, 82, 108).ToArgb()
            };
        }

        private static string Escape(string value) { return Uri.EscapeDataString(value ?? ""); }
        private static string Unescape(string value) { try { return Uri.UnescapeDataString(value ?? ""); } catch { return value ?? ""; } }
        private static string JoinDoubles(double[] values)
        {
            string[] result = new string[4]; for (int i = 0; i < 4; i++) result[i] = values[i].ToString("R", CultureInfo.InvariantCulture); return String.Join(",", result);
        }
        private static string JoinInts(int[] values)
        {
            string[] result = new string[5]; for (int i = 0; i < 5; i++) result[i] = values[i].ToString(CultureInfo.InvariantCulture); return String.Join(",", result);
        }
        private static double[] ParseDoubles(string value, double[] fallback)
        {
            string[] p = value.Split(','); if (p.Length != 4) return fallback; double[] result = new double[4];
            for (int i = 0; i < 4; i++) if (!Double.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])) return fallback;
            return result;
        }
        private static int[] ParseInts(string value, int[] fallback)
        {
            string[] p = value.Split(','); if (p.Length != 5) return fallback; int[] result = new int[5];
            for (int i = 0; i < 5; i++) if (!Int32.TryParse(p[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i])) return fallback;
            return result;
        }
    }

    internal static class DashboardDefaults
    {
        public const int Rows = 14;

        public static List<DashboardItem> Create(int columns)
        {
            List<DashboardItem> items = new List<DashboardItem>();
            items.Add(Section(columns, 0, "CPU · Intel Xeon X5670", Color.FromArgb(80, 181, 255)));
            items.Add(Role("CpuTemp", "CORE MAX", DashboardBoxType.Big, 0, 1, columns == 3));
            items.Add(Role("CpuLoad", "UTILIZATION", DashboardBoxType.Big, 1, 1, columns == 3));
            items.Add(Ram(2, 1, columns == 3));
            for (int column = 0; column < 3; column++)
            {
                items.Add(Role("CpuCore" + (column * 2), columns == 4 ? "C" + (column * 2 + 1) : "CORE " + (column * 2 + 1), DashboardBoxType.Horizontal, column, 3, false));
                items.Add(Role("CpuCore" + (column * 2 + 1), columns == 4 ? "C" + (column * 2 + 2) : "CORE " + (column * 2 + 2), DashboardBoxType.Horizontal, column, 4, false));
            }
            DashboardItem cpuGraph = Role("CpuPower", "CPU VRM OUTPUT", DashboardBoxType.Graph, 0, 5, false); cpuGraph.ColumnSpan = columns; cpuGraph.GraphMinimum = 0; cpuGraph.GraphMaximum = 150; items.Add(cpuGraph);
            items.Add(Section(columns, 7, "GPU · Radeon RX Vega 64 · Gigabyte Gaming OC", Color.FromArgb(255, 98, 121)));
            items.Add(Role("GpuTemp", "GPU TEMP", DashboardBoxType.Big, 0, 8, columns == 3));
            items.Add(Role("GpuHotspot", "HOT", DashboardBoxType.Horizontal, 1, 8, false));
            items.Add(Role("GpuHbmTemp", "HBM", DashboardBoxType.Horizontal, 1, 9, false));
            items.Add(Role("GpuLoad", "UTILIZATION", DashboardBoxType.Big, 2, 8, columns == 3));
            if (columns == 4)
            {
                items.Add(Role("GpuMemory", "VRAM USED", DashboardBoxType.Big, 3, 8, false));
                items.Add(Role("GpuClock", "CORE CLOCK", DashboardBoxType.Vertical, 0, 10, false));
                items.Add(Role("GpuMemClock", "HBM CLOCK", DashboardBoxType.Vertical, 1, 10, false));
                items.Add(Role("GpuFan", "FAN", DashboardBoxType.Vertical, 2, 10, false));
            }
            else
            {
                items.Add(Role("GpuClock", "CORE CLOCK", DashboardBoxType.Vertical, 0, 10, false));
                items.Add(Role("GpuMemClock", "HBM CLOCK", DashboardBoxType.Vertical, 1, 10, false));
                items.Add(Role("GpuMemory", "VRAM USED", DashboardBoxType.Big, 2, 10, true));
            }
            DashboardItem gpuGraph = Role("GpuPower", "GPU POWER EST.", DashboardBoxType.Graph, 0, 12, false); gpuGraph.ColumnSpan = columns; gpuGraph.GraphMinimum = 0; gpuGraph.GraphMaximum = 350; items.Add(gpuGraph);
            return items;
        }

        public static DashboardItem ForSensor(SensorReading sensor, DashboardBoxType type, int columns)
        {
            DashboardItem item = new DashboardItem();
            item.BoxType = type; item.SensorKey = sensor == null ? "" : sensor.Key; item.SensorLabel = sensor == null ? "" : sensor.OriginalLabel;
            item.SensorName = sensor == null ? "" : sensor.SensorName; item.DisplayName = sensor == null ? "NEW SENSOR" : sensor.Label.ToUpperInvariant();
            item.ApplyTypeDefaults(columns); ApplyPalette(item, sensor == null ? "" : sensor.Unit, sensor == null ? 0 : sensor.Type, "");
            return item;
        }

        private static DashboardItem Section(int columns, int row, string name, Color color)
        {
            DashboardItem item = new DashboardItem(); item.BoxType = DashboardBoxType.Section; item.DisplayName = name; item.Row = row; item.ColumnSpan = columns; item.RowSpan = 1;
            item.Colors = new int[] { color.ToArgb(), color.ToArgb(), color.ToArgb(), color.ToArgb(), color.ToArgb() }; return item;
        }

        private static DashboardItem Ram(int column, int row, bool extrema)
        {
            DashboardItem item = new DashboardItem(); item.Id = "ram-" + column + "-" + row; item.SensorKey = "__RAM_USED__"; item.SensorLabel = "RAM used"; item.SensorName = "System memory";
            item.DisplayName = "RAM USED"; item.BoxType = DashboardBoxType.Big; item.Column = column; item.Row = row; item.ShowExtrema = extrema;
            item.Thresholds = new double[] { 4.8, 7.2, 9.0, 10.6 }; item.Colors = DashboardItem.AlertColors(); return item;
        }

        private static DashboardItem Role(string role, string name, DashboardBoxType type, int column, int row, bool extrema)
        {
            DashboardItem item = new DashboardItem(); item.Id = role.ToLowerInvariant() + "-" + column + "-" + row; item.SensorKey = "role:" + role; item.SensorLabel = role; item.DisplayName = name;
            item.BoxType = type; item.Column = column; item.Row = row; item.ShowExtrema = extrema; item.ApplyTypeDefaults(4); ApplyPalette(item, "", -1, role); return item;
        }

        public static void ApplyPalette(DashboardItem item, string unit, int sensorType, string role)
        {
            string u = unit == null ? "" : unit.Trim();
            item.Colors = DashboardItem.ActivityColors(); item.Thresholds = new double[] { 20, 40, 60, 80 };
            if (role == "CpuPower") { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 30, 60, 90, 120 }; }
            else if (role == "GpuPower") { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 70, 140, 210, 280 }; }
            else if (role == "CpuLoad" || role == "GpuLoad") { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 20, 45, 70, 90 }; }
            else if (u.Equals("°C", StringComparison.OrdinalIgnoreCase) || role.IndexOf("Temp", StringComparison.OrdinalIgnoreCase) >= 0)
            { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 45, 60, 75, 85 }; }
            else if (role == "GpuHotspot") { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 60, 75, 90, 100 }; }
            else if (role == "GpuHbmTemp") { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 55, 70, 82, 90 }; }
            else if (role == "GpuMemory") { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 2048, 4096, 6144, 7373 }; }
            else if (role.StartsWith("CpuCore", StringComparison.OrdinalIgnoreCase)) item.Thresholds = new double[] { 1200, 2400, 3200, 4000 };
            else if (role == "GpuClock") item.Thresholds = new double[] { 300, 800, 1200, 1500 };
            else if (role == "GpuMemClock") item.Thresholds = new double[] { 250, 500, 750, 900 };
            else if (role == "GpuFan" || u.Equals("RPM", StringComparison.OrdinalIgnoreCase)) item.Thresholds = new double[] { 500, 1000, 1600, 2200 };
            else if (u.Equals("W", StringComparison.OrdinalIgnoreCase)) item.Thresholds = new double[] { 50, 100, 200, 300 };
            else if (u.Equals("MHz", StringComparison.OrdinalIgnoreCase)) item.Thresholds = new double[] { 300, 800, 1200, 1500 };
            else if (u.Equals("%", StringComparison.OrdinalIgnoreCase)) { item.Colors = DashboardItem.AlertColors(); item.Thresholds = new double[] { 20, 45, 70, 90 }; }
        }
    }
}