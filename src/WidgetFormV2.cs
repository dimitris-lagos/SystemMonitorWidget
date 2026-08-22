using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal sealed class WidgetForm : Form
    {
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
        private const int DashboardTop = 62, SlotHeight = 31, RowGap = 5, ColumnGap = 8, BottomPadding = 11;
        private readonly HWiNFOReader reader = new HWiNFOReader();
        private readonly Timer timer = new Timer();
        private readonly ProcessUsageSampler processSampler = new ProcessUsageSampler();
        private WidgetConfig config;
        private List<SensorReading> readings = new List<SensorReading>();
        private List<ProcessUsage> topProcesses = new List<ProcessUsage>();
        private readonly Dictionary<string, List<double>> history = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MetricExtrema> extrema = new Dictionary<string, MetricExtrema>(StringComparer.OrdinalIgnoreCase);
        private readonly WidgetComponents components = new WidgetComponents();
        private double ramUsed, ramTotal; private bool ramAvailable;
        private string status = "Starting";
        private ContextMenuStrip menu; private ToolStripMenuItem topmostItem, scaleItem, gridItem, processItem;
        private const int WmNcHitTest = 0x0084, HtClient = 1, HtTransparent = -1;

        private float UiScale { get { if (config.UiScaleMode == 75) return 0.75f; if (config.UiScaleMode == 67) return 2f / 3f; if (config.UiScaleMode == 50) return 0.5f; if (config.UiScaleMode == 33) return 1f / 3f; if (config.UiScaleMode == 25) return 0.25f; return 1f; } }
        private int CanvasWidth { get { return config.Width; } }
        private int LogicalHeight { get { int rows = Math.Max(4, config.ActiveDashboardRows); return DashboardTop + rows * SlotHeight + Math.Max(0, rows - 1) * RowGap + BottomPadding; } }

        public WidgetForm()
        {
            config = WidgetConfig.Load(); FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true; BackColor = Color.FromArgb(13, 17, 23); ForeColor = Color.White;
            ApplyWidgetSize(); Location = ClampLocation(new Point(config.Left, config.Top));
            TopMost = config.AlwaysOnTop; Opacity = config.OpacityPercent / 100.0;
            BuildMenu(); timer.Interval = config.RefreshMilliseconds; timer.Tick += delegate { RefreshSensors(); }; timer.Start();
            Shown += delegate { RefreshSensors(); }; FormClosing += delegate { config.Left = Left; config.Top = Top; config.Save(); };
            MouseDown += GearMouseDown;
            if (config.LaunchHWiNFO) LaunchHWiNFO();
        }

        private void BuildMenu()
        {
            menu = new ContextMenuStrip(); menu.Items.Add("Configure dashboard…", null, delegate { ShowSettings(); }); menu.Items.Add("Refresh now", null, delegate { RefreshSensors(); });
            topmostItem = new ToolStripMenuItem("Always on top"); topmostItem.Checked = config.AlwaysOnTop;
            topmostItem.Click += delegate { config.AlwaysOnTop = !config.AlwaysOnTop; TopMost = config.AlwaysOnTop; topmostItem.Checked = config.AlwaysOnTop; config.Save(); };
            menu.Items.Add(topmostItem); gridItem = new ToolStripMenuItem("Grid layout"); AddGridMenuItem("3 columns", 3); AddGridMenuItem("4 columns", 4); UpdateGridMenu(); menu.Items.Add(gridItem);
            scaleItem = new ToolStripMenuItem("UI scale"); AddScaleMenuItem("100% (1/1)", 100); AddScaleMenuItem("75% (3/4)", 75); AddScaleMenuItem("67% (2/3)", 67); AddScaleMenuItem("50% (1/2)", 50); AddScaleMenuItem("33% (1/3)", 33); AddScaleMenuItem("25% (1/4)", 25); UpdateScaleMenu(); menu.Items.Add(scaleItem);
            processItem = new ToolStripMenuItem("Header processes"); AddProcessMenuItem("No", 0); AddProcessMenuItem("Top CPU", 1); AddProcessMenuItem("Top RAM", 2); UpdateProcessMenu(); menu.Items.Add(processItem);
            menu.Items.Add("Start HWiNFO", null, delegate { LaunchHWiNFO(); }); menu.Items.Add("Reset position", null, delegate { Location = new Point(60, 60); });
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit", null, delegate { Close(); });
        }

        private void AddGridMenuItem(string text, int columns) { ToolStripMenuItem item = new ToolStripMenuItem(text); item.Tag = columns; item.Click += delegate { SetGridColumns(columns); }; gridItem.DropDownItems.Add(item); }
        private void SetGridColumns(int columns) { config.GridColumns = columns == 3 ? 3 : 4; ApplyWidgetSize(); Location = ClampLocation(Location); UpdateGridMenu(); config.Save(); RefreshSensors(); }
        private void UpdateGridMenu() { if (gridItem == null) return; foreach (ToolStripItem raw in gridItem.DropDownItems) { ToolStripMenuItem item = raw as ToolStripMenuItem; if (item != null) item.Checked = (int)item.Tag == config.GridColumns; } }
        private void AddScaleMenuItem(string text, int mode) { ToolStripMenuItem item = new ToolStripMenuItem(text); item.Tag = mode; item.Click += delegate { SetUiScale(mode); }; scaleItem.DropDownItems.Add(item); }
        private void SetUiScale(int mode) { config.UiScaleMode = mode == 75 || mode == 67 || mode == 50 || mode == 33 || mode == 25 ? mode : 100; ApplyWidgetSize(); Location = ClampLocation(Location); UpdateScaleMenu(); config.Save(); Invalidate(); }
        private void UpdateScaleMenu() { if (scaleItem == null) return; foreach (ToolStripItem raw in scaleItem.DropDownItems) { ToolStripMenuItem item = raw as ToolStripMenuItem; if (item != null) item.Checked = (int)item.Tag == config.UiScaleMode; } }
        private void AddProcessMenuItem(string text, int mode) { ToolStripMenuItem item = new ToolStripMenuItem(text); item.Tag = mode; item.Click += delegate { SetProcessMode(mode); }; processItem.DropDownItems.Add(item); }
        private void SetProcessMode(int mode) { config.ProcessStripMode = Math.Max(0, Math.Min(2, mode)); topProcesses.Clear(); UpdateProcessMenu(); config.Save(); RefreshSensors(); }
        private void UpdateProcessMenu() { if (processItem == null) return; foreach (ToolStripItem raw in processItem.DropDownItems) { ToolStripMenuItem item = raw as ToolStripMenuItem; if (item != null) item.Checked = (int)item.Tag == config.ProcessStripMode; } }
        private void ApplyWidgetSize() { float scale = UiScale; Size scaled = new Size(Math.Max(1, (int)Math.Round(config.Width * scale)), Math.Max(1, (int)Math.Round(LogicalHeight * scale))); MinimumSize = Size.Empty; MaximumSize = Size.Empty; Size = scaled; MinimumSize = scaled; MaximumSize = scaled; }

        private void RefreshSensors()
        {
            readings = reader.Read(out status); ramAvailable = PhysicalMemory.Read(out ramUsed, out ramTotal);
            if (config.ProcessStripMode == 0) topProcesses.Clear(); else topProcesses = processSampler.SampleTop(3, config.ProcessStripMode == 1);
            foreach (DashboardItem item in config.ActiveDashboard)
            {
                if (item.BoxType == DashboardBoxType.Section) continue;
                double? value = ItemValue(item); if (!value.HasValue) continue;
                GetExtrema(item.Id).Add(value.Value);
                if (item.BoxType == DashboardBoxType.Graph) AddHistory(item.Id, value.Value);
            }
            Invalidate();
        }

        private MetricExtrema GetExtrema(string key) { MetricExtrema value; if (!extrema.TryGetValue(key, out value)) { value = new MetricExtrema(); extrema[key] = value; } return value; }
        private void AddHistory(string key, double value) { List<double> values; if (!history.TryGetValue(key, out values)) { values = new List<double>(); history[key] = values; } values.Add(value); while (values.Count > 90) values.RemoveAt(0); }

        private SensorReading Resolve(DashboardItem item)
        {
            if (item == null || item.SensorKey == "__RAM_USED__" || item.SensorKey.Length == 0) return null;
            if (item.SensorKey.StartsWith("role:", StringComparison.OrdinalIgnoreCase)) return RoleDefinitions.Resolve(readings, config, item.SensorKey.Substring(5));
            SensorReading exact = readings.Find(delegate(SensorReading r) { return r.Key.Equals(item.SensorKey, StringComparison.OrdinalIgnoreCase); });
            if (exact != null) return exact;
            return readings.Find(delegate(SensorReading r) {
                bool label = r.OriginalLabel.Equals(item.SensorLabel, StringComparison.OrdinalIgnoreCase) || r.Label.Equals(item.SensorLabel, StringComparison.OrdinalIgnoreCase);
                return label && (item.SensorName.Length == 0 || r.SensorName.Equals(item.SensorName, StringComparison.OrdinalIgnoreCase));
            });
        }

        private double? ItemValue(DashboardItem item)
        {
            if (item.SensorKey == "__RAM_USED__") return ramAvailable ? (double?)ramUsed : null;
            SensorReading reading = Resolve(item); return reading == null || Double.IsNaN(reading.Value) || Double.IsInfinity(reading.Value) ? (double?)null : reading.Value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; g.ScaleTransform(UiScale, UiScale);
            Rectangle outer = new Rectangle(0, 0, CanvasWidth - 1, LogicalHeight - 1);
            using (GraphicsPath path = Rounded(outer, 14)) { using (SolidBrush b = new SolidBrush(Color.FromArgb(13, 17, 23))) g.FillPath(b, path); using (Pen p = new Pen(Color.FromArgb(48, 58, 71))) g.DrawPath(p, path); }
            DrawHeader(g); DrawDashboard(g); g.ResetTransform(); DrawGearButton(g);
            using (GraphicsPath physicalPath = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Math.Max(2, (int)Math.Round(14 * UiScale)))) Region = new Region(physicalPath);
        }

        private void DrawHeader(Graphics g)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(1, 1, CanvasWidth - 2, 54), Color.FromArgb(31, 39, 51), Color.FromArgb(21, 27, 36), 90f)) g.FillRectangle(b, 1, 1, CanvasWidth - 2, 54);
            DrawTextFit(g, config.HeaderTitle, 13.5f, 9f, FontStyle.Bold, Color.White, new RectangleF(16, 10, CanvasWidth - 150, 24), StringAlignment.Near);

            DrawText(g, FormatSystemUptime(), 8, FontStyle.Bold, Color.FromArgb(180, 194, 210), new RectangleF(CanvasWidth - 98, 12, 86, 18), StringAlignment.Far);
            DrawProcessStrip(g, new RectangleF(12, 34, CanvasWidth - 24, 14));
        }

        private static string FormatSystemUptime()
        {
            TimeSpan uptime = TimeSpan.FromMilliseconds(GetTickCount64());
            if (uptime.TotalDays >= 1) return "UP " + (int)uptime.TotalDays + "d " + uptime.Hours + "h";
            if (uptime.TotalHours >= 1) return "UP " + (int)uptime.TotalHours + "h " + uptime.Minutes + "m";
            return "UP " + Math.Max(0, uptime.Minutes) + "m";
        }
        private void DrawProcessStrip(Graphics g, RectangleF area)
        {
            if (config.ProcessStripMode == 0) return;
            if (topProcesses.Count == 0)
            {
                DrawText(g, "Collecting process activity", 7f, FontStyle.Regular, Color.FromArgb(135, 148, 164), area, StringAlignment.Near);
                return;
            }
            int count = Math.Min(3, topProcesses.Count);
            float cellWidth = area.Width / count;
            for (int i = 0; i < count; i++)
            {
                RectangleF cell = new RectangleF(area.X + i * cellWidth + 3, area.Y, cellWidth - 6, area.Height);
                DrawTextFit(g, topProcesses[i].CompactText, 8.2f, 6.25f, FontStyle.Bold, Color.FromArgb(173, 188, 207), cell, StringAlignment.Center);
                if (i < count - 1) using (SolidBrush dot = new SolidBrush(Color.FromArgb(64, 183, 255))) g.FillEllipse(dot, area.X + (i + 1) * cellWidth - 1.5f, area.Y + 6f, 3f, 3f);
            }
        }

        private static void DrawTextFit(Graphics g, string text, float preferredSize, float minimumSize, FontStyle style, Color color, RectangleF area, StringAlignment align)
        {
            float size = preferredSize;
            while (size > minimumSize)
            {
                using (Font probe = new Font("Segoe UI", size, style, GraphicsUnit.Point))
                    if (g.MeasureString(text ?? "", probe).Width <= area.Width) break;
                size -= 0.25f;
            }
            DrawText(g, text, Math.Max(minimumSize, size), style, color, area, align);
        }

        private void DrawDashboard(Graphics g)
        {
            foreach (DashboardItem item in config.ActiveDashboard)
            {
                Rectangle r = ItemRectangle(item);
                if (r.Width <= 0 || r.Height <= 0) continue;
                if (item.BoxType == DashboardBoxType.Section) { DrawSection(g, r, item); continue; }
                float valueFontScale = Math.Max(0.6f, Math.Min(1.6f, item.ValueFontPercent / 100f));
                SensorReading reading = Resolve(item); double? raw = ItemValue(item);
                Color color = raw.HasValue ? item.ColorFor(raw.Value) : Color.FromArgb(85, 96, 110);
                string value = item.SensorKey == "__RAM_USED__" ? (ramAvailable ? SensorValueFormatter.FormatValue(ramUsed, "GB", item.ValueDecimals, item.ShowUnit) : "—") : SensorValueFormatter.FormatReading(reading, item.ValueDecimals, item.ShowUnit);
                string label = String.IsNullOrWhiteSpace(item.DisplayName) ? (reading == null ? "UNASSIGNED" : reading.Label.ToUpperInvariant()) : item.DisplayName.Trim().ToUpperInvariant();
                if (item.BoxType == DashboardBoxType.Big)
                {
                    MetricExtrema range = GetExtrema(item.Id);
                    string max = FormatExtrema(item, reading, range, true), min = FormatExtrema(item, reading, range, false);
                    components.DrawBigMetric(g, r, label, value, max, min, color, item.ShowExtrema, valueFontScale);
                }
                else if (item.BoxType == DashboardBoxType.Horizontal) components.DrawHorizontalSpec(g, r, label, value, color, valueFontScale);
                else if (item.BoxType == DashboardBoxType.Vertical) components.DrawVerticalSpec(g, r, label, value, color, valueFontScale);
                else if (item.BoxType == DashboardBoxType.Graph)
                {
                    List<double> values = null; if (config.ShowGraphs) history.TryGetValue(item.Id, out values);
                    string unit = !item.ShowUnit || reading == null || reading.Unit == null ? "" : reading.Unit.Trim();
                    string range = FormatRange(item.GraphMinimum, item.GraphMaximum, unit);
                    components.DrawGraphBox(g, r, label, value, range, color, values, item.GraphMinimum, item.GraphMaximum, valueFontScale);
                }
            }
        }

        private Rectangle ItemRectangle(DashboardItem item)
        {
            int columns = config.GridColumns, gridX = 12, gridWidth = CanvasWidth - 24;
            int cellWidth = (gridWidth - ColumnGap * (columns - 1)) / columns;
            if (item.Column < 0 || item.Column >= columns || item.Row < 0 || item.Row >= config.ActiveDashboardRows) return Rectangle.Empty;
            int span = Math.Max(1, Math.Min(columns - item.Column, item.ColumnSpan));
            int rows = Math.Max(1, Math.Min(config.ActiveDashboardRows - item.Row, item.RowSpan));
            int x = gridX + item.Column * (cellWidth + ColumnGap);
            int right = item.Column + span == columns ? gridX + gridWidth : x + span * cellWidth + (span - 1) * ColumnGap;
            int y = DashboardTop + item.Row * (SlotHeight + RowGap);
            int height = rows * SlotHeight + (rows - 1) * RowGap;
            return Rectangle.FromLTRB(x, y, right, y + height);
        }

        private void DrawSection(Graphics g, Rectangle r, DashboardItem item)
        {
            Color accent = Color.FromArgb(item.Colors[0]);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(23, 29, 38))) g.FillRectangle(b, r);
            using (SolidBrush b = new SolidBrush(accent)) g.FillRectangle(b, r.X, r.Y + 4, 3, r.Height - 8);
            DrawText(g, item.DisplayName, 9.5f, FontStyle.Bold, Color.FromArgb(225, 232, 240), new RectangleF(r.X + 12, r.Y + 2, r.Width - 18, r.Height - 4), StringAlignment.Near);
        }

        private string FormatExtrema(DashboardItem item, SensorReading reading, MetricExtrema range, bool maximum)
        {
            if (range == null || !range.HasValue) return "—"; double value = maximum ? range.Maximum : range.Minimum;
            if (item.SensorKey == "__RAM_USED__") return SensorValueFormatter.FormatValue(value, "GB", item.ValueDecimals, item.ShowUnit);
            return SensorValueFormatter.FormatValue(value, reading == null ? "" : reading.Unit, item.ValueDecimals, item.ShowUnit);
        }

        private static string FormatRange(double minimum, double maximum, string unit)
        {
            string suffix = String.IsNullOrWhiteSpace(unit) ? "" : " " + unit.Trim();
            return minimum.ToString("0.##", CultureInfo.InvariantCulture) + "–" + maximum.ToString("0.##", CultureInfo.InvariantCulture) + suffix;
        }

        private static void DrawText(Graphics g, string text, float size, FontStyle style, Color color, RectangleF area, StringAlignment align)
        {
            using (Font f = new Font("Segoe UI", size, style, GraphicsUnit.Point)) using (SolidBrush b = new SolidBrush(color)) using (StringFormat sf = new StringFormat())
            { sf.Alignment = align; sf.LineAlignment = StringAlignment.Center; sf.Trimming = StringTrimming.None; sf.FormatFlags = StringFormatFlags.NoWrap; g.DrawString(text ?? "", f, b, area, sf); }
        }
        private static GraphicsPath Rounded(Rectangle r, int radius) { GraphicsPath p = new GraphicsPath(); int d = radius * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
        private Rectangle GearBounds
        {
            get
            {
                int size = Math.Max(14, (int)Math.Round(20 * UiScale));
                int centerX = (int)Math.Round((CanvasWidth - 108) * UiScale), centerY = Math.Max(size / 2 + 2, (int)Math.Round(21 * UiScale));
                return new Rectangle(centerX - size / 2, centerY - size / 2, size, size);
            }
        }
        private void DrawGearButton(Graphics g)
        {
            Rectangle r = GearBounds; bool live = readings.Count > 0; Color accent = live ? Color.FromArgb(59, 214, 113) : Color.FromArgb(255, 184, 77);
            using (SolidBrush background = new SolidBrush(Color.FromArgb(175, 23, 30, 39))) g.FillEllipse(background, r);
            using (Pen border = new Pen(Color.FromArgb(155, accent), 1f)) g.DrawEllipse(border, r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
            using (Font font = new Font("Segoe UI Symbol", Math.Max(8f, 11f * UiScale), FontStyle.Regular, GraphicsUnit.Point))
            using (SolidBrush brush = new SolidBrush(accent))
            using (StringFormat format = new StringFormat()) { format.Alignment = StringAlignment.Center; format.LineAlignment = StringAlignment.Center; g.DrawString("\u2699", font, brush, r, format); }
        }
        private void GearMouseDown(object sender, MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) && GearBounds.Contains(e.Location)) menu.Show(this, new Point(GearBounds.Left, GearBounds.Bottom + 2));
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest)
            {
                int packed = unchecked((int)(long)m.LParam); Point screen = new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)); Point client = PointToClient(screen);
                m.Result = GearBounds.Contains(client) ? (IntPtr)HtClient : (IntPtr)HtTransparent; return;
            }
            base.WndProc(ref m);
        }
        private Point ClampLocation(Point p) { Rectangle work = Screen.PrimaryScreen.WorkingArea; return new Point(Math.Max(work.Left, Math.Min(work.Right - Width, p.X)), Math.Max(work.Top, Math.Min(work.Bottom - Height, p.Y))); }
        private void ShowSettings() { using (SettingsForm form = new SettingsForm(config, readings)) { if (form.ShowDialog(this) != DialogResult.OK) return; config = form.Result; ApplyWidgetSize(); Location = ClampLocation(Location); TopMost = config.AlwaysOnTop; Opacity = config.OpacityPercent / 100.0; timer.Interval = config.RefreshMilliseconds; topmostItem.Checked = config.AlwaysOnTop; UpdateGridMenu(); UpdateScaleMenu(); config.Save(); RefreshSensors(); } }
        private void LaunchHWiNFO() { try { if (Process.GetProcessesByName("HWiNFO64").Length > 0) return; string path = @"C:\Program Files\HWiNFO64\HWiNFO64.EXE"; if (File.Exists(path)) Process.Start(path); else { status = "HWiNFO64 was not found"; Invalidate(); } } catch { status = "Could not start HWiNFO"; Invalidate(); } }
    }
}