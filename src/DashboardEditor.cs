using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal sealed class SensorChoice
    {
        public string Key, Label, SensorName, Unit, Display, SearchText;
        public SensorReading Reading;
        public override string ToString() { return Display; }

        public bool Matches(string query)
        {
            if (String.IsNullOrWhiteSpace(query)) return true;
            string[] terms = query.ToLowerInvariant().Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string searchable = SearchText ?? "";
            foreach (string term in terms) if (searchable.IndexOf(term, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        private static string TypeAliases(SensorReading reading)
        {
            string unit = (reading.Unit ?? "").Trim().ToLowerInvariant();
            switch (reading.Type)
            {
                case 1: return "temperature temp thermal celsius degrees";
                case 2: return "voltage volt volts";
                case 3: return "fan rpm speed tachometer rotation";
                case 4: return "current amp amps ampere";
                case 5: return "power watt watts consumption";
                case 6: return "clock frequency mhz ghz";
                case 7: return "usage utilization load percent";
            }
            if (unit == "rpm") return "fan rpm speed tachometer rotation";
            if (unit == "w") return "power watt watts consumption";
            if (unit == "mhz" || unit == "ghz") return "clock frequency mhz ghz";
            if (unit == "%") return "usage utilization load percent";
            return "other sensor reading";
        }

        public static List<SensorChoice> Build(List<SensorReading> readings)
        {
            List<SensorChoice> result = new List<SensorChoice>();
            result.Add(new SensorChoice { Key = "__RAM_USED__", Label = "RAM used", SensorName = "System memory", Unit = "GB", Display = "RAM used  —  System memory  [GB]", SearchText = "ram used system memory gb utilization usage" });
            foreach (SensorReading reading in readings)
            {
                string display = reading.Label + "  —  " + reading.SensorName + (String.IsNullOrWhiteSpace(reading.Unit) ? "" : "  [" + reading.Unit.Trim() + "]");
                string search = String.Join(" ", new string[] { reading.Label, reading.OriginalLabel, reading.SensorName, reading.Unit, TypeAliases(reading) }).ToLowerInvariant();
                result.Add(new SensorChoice {
                    Key = reading.Key, Label = reading.OriginalLabel, SensorName = reading.SensorName, Unit = reading.Unit, Reading = reading,
                    Display = display, SearchText = search
                });
            }
            result.Sort(delegate(SensorChoice a, SensorChoice b) { if (a.Key == "__RAM_USED__") return -1; if (b.Key == "__RAM_USED__") return 1; return String.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase); });
            return result;
        }
    }
    internal sealed class DashboardCanvas : Control
    {
        private const int MarginSize = 8, ColumnGap = 7, RowGap = 5, SlotHeight = 31;
        private List<DashboardItem> items = new List<DashboardItem>();
        private DashboardItem selected, dragItem;
        private int columns = 4, rows = 14, previewColumn, previewRow;
        private bool dragging, previewValid;
        public event EventHandler SelectionChanged;
        public event EventHandler LayoutChanged;

        public DashboardCanvas()
        {
            DoubleBuffered = true; BackColor = Color.FromArgb(18, 23, 31); Cursor = Cursors.Default; MinimumSize = new Size(480, 300);
            MouseDown += CanvasMouseDown; MouseMove += CanvasMouseMove; MouseUp += CanvasMouseUp;
        }

        public DashboardItem SelectedItem { get { return selected; } }
        public int Columns { get { return columns; } }
        public int Rows { get { return rows; } }

        public void SetLayout(List<DashboardItem> value, int columnCount, int rowCount)
        {
            items = value ?? new List<DashboardItem>(); columns = columnCount == 3 ? 3 : 4; rows = Math.Max(4, Math.Min(30, rowCount));
            Height = MarginSize * 2 + rows * SlotHeight + Math.Max(0, rows - 1) * RowGap; selected = null; Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public void SelectItem(DashboardItem item)
        {
            selected = item; Invalidate(); if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public bool IsPlacementFree(DashboardItem item, int column, int row, int columnSpan, int rowSpan)
        {
            if (column < 0 || row < 0 || columnSpan < 1 || rowSpan < 1 || column + columnSpan > columns || row + rowSpan > rows) return false;
            Rectangle candidate = new Rectangle(column, row, columnSpan, rowSpan);
            foreach (DashboardItem other in items)
            {
                if (Object.ReferenceEquals(other, item)) continue;
                Rectangle occupied = new Rectangle(other.Column, other.Row, Math.Max(1, other.ColumnSpan), Math.Max(1, other.RowSpan));
                if (candidate.IntersectsWith(occupied)) return false;
            }
            return true;
        }

        public bool FindFirstFree(DashboardItem item, out int column, out int row)
        {
            for (int r = 0; r <= rows - item.RowSpan; r++)
                for (int c = 0; c <= columns - item.ColumnSpan; c++)
                    if (IsPlacementFree(item, c, r, item.ColumnSpan, item.RowSpan)) { column = c; row = r; return true; }
            column = 0; row = 0; return false;
        }

        public bool TryPlace(DashboardItem item, int column, int row, int columnSpan, int rowSpan)
        {
            if (!IsPlacementFree(item, column, row, columnSpan, rowSpan)) return false;
            item.Column = column; item.Row = row; item.ColumnSpan = columnSpan; item.RowSpan = rowSpan; Invalidate();
            if (LayoutChanged != null) LayoutChanged(this, EventArgs.Empty); return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen grid = new Pen(Color.FromArgb(40, 51, 64))) { grid.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot; for (int r = 0; r < rows; r++) for (int c = 0; c < columns; c++) g.DrawRectangle(grid, GridRectangle(c, r, 1, 1)); }
            foreach (DashboardItem item in items)
            {
                int c = item.Column, r = item.Row; bool isDrag = dragging && Object.ReferenceEquals(item, dragItem);
                if (isDrag) { c = previewColumn; r = previewRow; }
                Rectangle box = GridRectangle(c, r, item.ColumnSpan, item.RowSpan);
                DrawItem(g, box, item, Object.ReferenceEquals(item, selected), isDrag && !previewValid);
            }
            if (items.Count == 0) DrawCentered(g, "Add a component to begin", 11f, Color.FromArgb(120, 135, 152));
        }

        private void DrawItem(Graphics g, Rectangle r, DashboardItem item, bool isSelected, bool invalid)
        {
            Color accent = invalid ? Color.FromArgb(255, 82, 108) : Color.FromArgb(item.Colors[0]);
            using (SolidBrush fill = new SolidBrush(item.BoxType == DashboardBoxType.Section ? Color.FromArgb(29, 37, 48) : Color.FromArgb(13, 18, 25))) g.FillRectangle(fill, r);
            using (Pen border = new Pen(isSelected ? Color.White : accent, isSelected ? 2f : 1f)) g.DrawRectangle(border, r);
            using (SolidBrush stripe = new SolidBrush(accent)) g.FillRectangle(stripe, r.X, r.Y, 3, r.Height);
            string type = item.BoxType == DashboardBoxType.Horizontal ? "H-SPEC" : item.BoxType == DashboardBoxType.Vertical ? "V-SPEC" : item.BoxType.ToString().ToUpperInvariant();
            DrawText(g, type, 6.5f, FontStyle.Bold, Color.FromArgb(108, 124, 143), new RectangleF(r.X + 8, r.Y + 3, r.Width - 14, 12), StringAlignment.Near);
            DrawText(g, item.DisplayName, item.BoxType == DashboardBoxType.Section ? 9f : 8f, FontStyle.Bold, Color.FromArgb(225, 232, 240), new RectangleF(r.X + 8, r.Y + (item.RowSpan == 1 ? 11 : 16), r.Width - 14, Math.Max(14, r.Height - 18)), StringAlignment.Near);
        }

        private Rectangle GridRectangle(int column, int row, int columnSpan, int rowSpan)
        {
            int usable = Math.Max(10, Width - MarginSize * 2), cellWidth = (usable - ColumnGap * (columns - 1)) / columns;
            int x = MarginSize + column * (cellWidth + ColumnGap), y = MarginSize + row * (SlotHeight + RowGap);
            int right = column + columnSpan == columns ? Width - MarginSize : x + columnSpan * cellWidth + (columnSpan - 1) * ColumnGap;
            int height = rowSpan * SlotHeight + (rowSpan - 1) * RowGap;
            return Rectangle.FromLTRB(x, y, right, y + height);
        }

        private Point PointToCell(Point point)
        {
            int usable = Math.Max(10, Width - MarginSize * 2), cellWidth = (usable - ColumnGap * (columns - 1)) / columns;
            int column = (point.X - MarginSize + ColumnGap / 2) / Math.Max(1, cellWidth + ColumnGap);
            int row = (point.Y - MarginSize + RowGap / 2) / (SlotHeight + RowGap);
            return new Point(Math.Max(0, Math.Min(columns - 1, column)), Math.Max(0, Math.Min(rows - 1, row)));
        }

        private DashboardItem HitTest(Point point)
        {
            for (int i = items.Count - 1; i >= 0; i--) if (GridRectangle(items[i].Column, items[i].Row, items[i].ColumnSpan, items[i].RowSpan).Contains(point)) return items[i];
            return null;
        }

        private void CanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return; DashboardItem hit = HitTest(e.Location); SelectItem(hit);
            if (hit != null) { dragItem = hit; dragging = true; previewColumn = hit.Column; previewRow = hit.Row; previewValid = true; Capture = true; Cursor = Cursors.SizeAll; }
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || dragItem == null) return; Point cell = PointToCell(e.Location);
            previewColumn = Math.Max(0, Math.Min(columns - dragItem.ColumnSpan, cell.X)); previewRow = Math.Max(0, Math.Min(rows - dragItem.RowSpan, cell.Y));
            previewValid = IsPlacementFree(dragItem, previewColumn, previewRow, dragItem.ColumnSpan, dragItem.RowSpan); Invalidate();
        }

        private void CanvasMouseUp(object sender, MouseEventArgs e)
        {
            if (!dragging) return; dragging = false; Capture = false; Cursor = Cursors.Default;
            if (dragItem != null && previewValid) { dragItem.Column = previewColumn; dragItem.Row = previewRow; if (LayoutChanged != null) LayoutChanged(this, EventArgs.Empty); }
            dragItem = null; Invalidate(); if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        private void DrawCentered(Graphics g, string text, float size, Color color)
        {
            DrawText(g, text, size, FontStyle.Regular, color, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
        }
        private static void DrawText(Graphics g, string text, float size, FontStyle style, Color color, RectangleF area, StringAlignment align)
        {
            using (Font font = new Font("Segoe UI", size, style)) using (SolidBrush brush = new SolidBrush(color)) using (StringFormat format = new StringFormat())
            { format.Alignment = align; format.LineAlignment = StringAlignment.Center; format.Trimming = StringTrimming.EllipsisCharacter; format.FormatFlags = StringFormatFlags.NoWrap; g.DrawString(text ?? "", font, brush, area, format); }
        }
    }

    internal sealed class AddComponentDialog : Form
    {
        private readonly TextBox search = new TextBox();
        private readonly ListBox sensor = new ListBox();
        private readonly ComboBox type = new ComboBox();
        private readonly Label resultCount = new Label();
        private readonly Button add = new Button();
        private readonly List<SensorChoice> choices;
        public SensorChoice SelectedSensor { get { return sensor.SelectedItem as SensorChoice; } }
        public DashboardBoxType SelectedType { get { return (DashboardBoxType)Math.Max(0, type.SelectedIndex); } }

        public AddComponentDialog(List<SensorChoice> available)
        {
            choices = available ?? new List<SensorChoice>(); Text = "Add dashboard component"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(820, 510); MinimumSize = new Size(720, 430); Font = new Font("Segoe UI", 9f);
            TableLayoutPanel table = new TableLayoutPanel(); table.Dock = DockStyle.Fill; table.Padding = new Padding(18); table.ColumnCount = 2; table.RowCount = 5; table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            table.Controls.Add(LabelFor("Find sensor"), 0, 0); search.Dock = DockStyle.Fill; search.Margin = new Padding(3, 4, 3, 6); table.Controls.Add(search, 1, 0);
            table.Controls.Add(LabelFor("Sensors"), 0, 1); sensor.Dock = DockStyle.Fill; sensor.IntegralHeight = false; sensor.HorizontalScrollbar = true; table.Controls.Add(sensor, 1, 1);
            resultCount.Dock = DockStyle.Fill; resultCount.ForeColor = Color.DimGray; resultCount.TextAlign = ContentAlignment.MiddleLeft; table.Controls.Add(resultCount, 1, 2);
            table.Controls.Add(LabelFor("Box type"), 0, 3); type.Dock = DockStyle.Left; type.Width = 260; type.DropDownStyle = ComboBoxStyle.DropDownList; type.Items.AddRange(new object[] { "Big metric", "Horizontal spec (half-cell)", "Vertical spec", "Graph", "Section heading" }); type.SelectedIndex = 0; table.Controls.Add(type, 1, 3);
            FlowLayoutPanel buttons = new FlowLayoutPanel(); buttons.FlowDirection = FlowDirection.RightToLeft; buttons.Dock = DockStyle.Fill; Button cancel = new Button(); cancel.Text = "Cancel"; cancel.DialogResult = DialogResult.Cancel; cancel.Size = new Size(90, 30); add.Text = "Add"; add.DialogResult = DialogResult.OK; add.Size = new Size(90, 30); buttons.Controls.Add(cancel); buttons.Controls.Add(add); table.Controls.Add(buttons, 0, 4); table.SetColumnSpan(buttons, 2); Controls.Add(table); AcceptButton = add; CancelButton = cancel;
            search.TextChanged += delegate { ApplyFilter(); }; sensor.SelectedIndexChanged += delegate { UpdateAddState(); }; sensor.DoubleClick += delegate { if (add.Enabled) { DialogResult = DialogResult.OK; Close(); } }; type.SelectedIndexChanged += delegate { bool needsSensor = SelectedType != DashboardBoxType.Section; search.Enabled = sensor.Enabled = needsSensor; UpdateAddState(); };
            ApplyFilter(); search.Select();
        }

        private void ApplyFilter()
        {
            SensorChoice selected = sensor.SelectedItem as SensorChoice; sensor.BeginUpdate(); sensor.Items.Clear();
            foreach (SensorChoice choice in choices) if (choice.Matches(search.Text)) sensor.Items.Add(choice);
            sensor.EndUpdate(); if (selected != null) sensor.SelectedItem = selected; if (sensor.SelectedIndex < 0 && sensor.Items.Count > 0) sensor.SelectedIndex = 0;
            resultCount.Text = sensor.Items.Count + " of " + choices.Count + " sensors"; UpdateAddState();
        }

        private void UpdateAddState() { add.Enabled = SelectedType == DashboardBoxType.Section || sensor.SelectedItem != null; }
        private static Label LabelFor(string text) { Label label = new Label(); label.Text = text; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; return label; }
    }
    internal sealed class DashboardEditorControl : UserControl
    {
        private readonly WidgetConfig config; private readonly List<SensorReading> readings; private readonly List<SensorChoice> choices;
        private readonly DashboardCanvas canvas = new DashboardCanvas(); private readonly SplitContainer split = new SplitContainer(); private readonly ComboBox layoutChoice = new ComboBox(), sensorChoice = new ComboBox(), typeChoice = new ComboBox();
        private readonly TextBox displayName = new TextBox(); private readonly NumericUpDown dashboardRows = Number(4, 30, 14), row = Number(1, 30, 1), column = Number(1, 4, 1), columnSpan = Number(1, 4, 1), graphMin = Number(-100000, 100000, 0), graphMax = Number(-100000, 100000, 100);
        private readonly CheckBox extrema = new CheckBox(); private readonly NumericUpDown[] thresholds = new NumericUpDown[4]; private readonly Button[] colorButtons = new Button[5];
        private readonly Label placementStatus = new Label(); private bool loading; private int editorColumns = 4;

        public DashboardEditorControl(WidgetConfig working, List<SensorReading> available)
        {
            config = working; readings = available ?? new List<SensorReading>(); choices = SensorChoice.Build(readings); Dock = DockStyle.Fill; BuildUi(); SwitchLayout(config.GridColumns);
        }

        public bool ValidateDashboard(out string error)
        {
            foreach (List<DashboardItem> list in new List<DashboardItem>[] { config.Dashboard3, config.Dashboard4 })
                foreach (DashboardItem item in list)
                {
                    if (item.BoxType != DashboardBoxType.Section && String.IsNullOrWhiteSpace(item.SensorKey)) { error = "Every non-section component must have a sensor."; return false; }
                    if (!(item.Thresholds[0] < item.Thresholds[1] && item.Thresholds[1] < item.Thresholds[2] && item.Thresholds[2] < item.Thresholds[3])) { error = "Color thresholds must increase from Step 2 through Step 5."; return false; }
                    if (item.BoxType == DashboardBoxType.Graph && item.GraphMaximum <= item.GraphMinimum) { error = "Every graph maximum must be higher than its minimum."; return false; }
                }
            error = ""; return true;
        }

        private void BuildUi()
        {
            Panel toolbar = new Panel(); toolbar.Dock = DockStyle.Top; toolbar.Height = 48; toolbar.Padding = new Padding(8);
            layoutChoice.DropDownStyle = ComboBoxStyle.DropDownList; layoutChoice.Items.AddRange(new object[] { "Edit 3-column dashboard", "Edit 4-column dashboard" }); layoutChoice.Width = 210; layoutChoice.Location = new Point(8, 10);
            Button add = ButtonFor("+ Add", 232); Button duplicate = ButtonFor("Duplicate", 330); Button delete = ButtonFor("Delete", 428); Button reset = ButtonFor("Reset layout", 526); Label rowsLabel = new Label(); rowsLabel.Text = "Rows"; rowsLabel.AutoSize = true; rowsLabel.Location = new Point(650, 16); dashboardRows.Location = new Point(688, 11); dashboardRows.Width = 58;
            toolbar.Controls.Add(layoutChoice); toolbar.Controls.Add(add); toolbar.Controls.Add(duplicate); toolbar.Controls.Add(delete); toolbar.Controls.Add(reset); toolbar.Controls.Add(rowsLabel); toolbar.Controls.Add(dashboardRows);
            split.Dock = DockStyle.Fill; split.Orientation = Orientation.Vertical; split.FixedPanel = FixedPanel.Panel2; split.SplitterWidth = 6;
            Panel scroll = new Panel(); scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; scroll.BackColor = Color.FromArgb(225, 230, 237); canvas.Dock = DockStyle.Top; scroll.Controls.Add(canvas); split.Panel1.Controls.Add(scroll);
            Panel properties = BuildProperties(); split.Panel2.Controls.Add(properties); Controls.Add(split); Controls.Add(toolbar);
            layoutChoice.SelectedIndexChanged += delegate { if (!loading) SwitchLayout(layoutChoice.SelectedIndex == 0 ? 3 : 4); };
            canvas.SelectionChanged += delegate { LoadSelected(); }; canvas.LayoutChanged += delegate { LoadSelected(); };
            add.Click += AddClick; duplicate.Click += DuplicateClick; delete.Click += DeleteClick; reset.Click += ResetClick; dashboardRows.ValueChanged += DashboardRowsChanged;
            Load += delegate { int available = Math.Max(800, split.ClientSize.Width - split.SplitterWidth); split.Panel1MinSize = 420; split.Panel2MinSize = 360; split.SplitterDistance = Math.Max(420, available - 360); };
        }

        private Panel BuildProperties()
        {
            Panel panel = new Panel(); panel.Dock = DockStyle.Fill; panel.AutoScroll = true; panel.Padding = new Padding(14);
            TableLayoutPanel table = new TableLayoutPanel(); table.Dock = DockStyle.Top; table.AutoSize = true; table.ColumnCount = 2; table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Heading(table, "SELECTED COMPONENT");
            AddRow(table, "Display name", displayName); displayName.MaxLength = 36;
            sensorChoice.DropDownStyle = ComboBoxStyle.DropDown; sensorChoice.AutoCompleteMode = AutoCompleteMode.SuggestAppend; sensorChoice.AutoCompleteSource = AutoCompleteSource.ListItems; sensorChoice.DropDownWidth = 850; foreach (SensorChoice choice in choices) sensorChoice.Items.Add(choice); AddRow(table, "Sensor", sensorChoice);
            typeChoice.DropDownStyle = ComboBoxStyle.DropDownList; typeChoice.Items.AddRange(new object[] { "Big metric", "Horizontal spec", "Vertical spec", "Graph", "Section heading" }); AddRow(table, "Box type", typeChoice);
            Heading(table, "POSITION & SIZE"); AddRow(table, "Column", column); AddRow(table, "Half-row", row); AddRow(table, "Width (cells)", columnSpan);
            extrema.Text = "Show minimum / maximum"; extrema.AutoSize = true; AddRow(table, "", extrema);
            Heading(table, "GRAPH RANGE"); AddRow(table, "Minimum", graphMin); AddRow(table, "Maximum", graphMax);
            Heading(table, "FIVE-STEP COLOR SCALE");
            Label hint = new Label(); hint.Text = "Step 1 applies below the first threshold. Each next color starts at the value shown."; hint.AutoSize = true; hint.MaximumSize = new Size(300, 0); hint.ForeColor = Color.DimGray; table.Controls.Add(hint); table.SetColumnSpan(hint, 2);
            for (int i = 0; i < 5; i++)
            {
                colorButtons[i] = new Button(); colorButtons[i].Text = "Step " + (i + 1); colorButtons[i].FlatStyle = FlatStyle.Flat; colorButtons[i].Height = 28; colorButtons[i].Dock = DockStyle.Fill; int index = i; colorButtons[i].Click += delegate { PickColor(index); };
                table.Controls.Add(colorButtons[i]);
                if (i == 0) { Label below = new Label(); below.Text = "lowest range"; below.Dock = DockStyle.Fill; below.TextAlign = ContentAlignment.MiddleLeft; below.ForeColor = Color.Gray; table.Controls.Add(below); }
                else { thresholds[i - 1] = Number(-100000, 100000, i * 20); table.Controls.Add(thresholds[i - 1]); int thresholdIndex = i - 1; thresholds[i - 1].ValueChanged += delegate { PropertyChanged(); }; }
            }
            placementStatus.AutoSize = true; placementStatus.ForeColor = Color.FromArgb(190, 68, 68); placementStatus.MaximumSize = new Size(300, 0); table.Controls.Add(placementStatus); table.SetColumnSpan(placementStatus, 2);
            panel.Controls.Add(table);
            displayName.TextChanged += delegate { PropertyChanged(); }; sensorChoice.SelectedIndexChanged += delegate { PropertyChanged(); }; typeChoice.SelectedIndexChanged += delegate { TypeChanged(); };
            row.ValueChanged += delegate { PositionChanged(); }; column.ValueChanged += delegate { PositionChanged(); }; columnSpan.ValueChanged += delegate { PositionChanged(); };
            extrema.CheckedChanged += delegate { PropertyChanged(); }; graphMin.ValueChanged += delegate { PropertyChanged(); }; graphMax.ValueChanged += delegate { PropertyChanged(); };
            return panel;
        }

        private void SwitchLayout(int columnsValue)
        {
            loading = true; editorColumns = columnsValue == 3 ? 3 : 4; layoutChoice.SelectedIndex = editorColumns == 3 ? 0 : 1;
            int activeRows = editorColumns == 3 ? config.DashboardRows3 : config.DashboardRows4; dashboardRows.Value = Clamp(dashboardRows, activeRows); canvas.SetLayout(editorColumns == 3 ? config.Dashboard3 : config.Dashboard4, editorColumns, activeRows);
            column.Maximum = editorColumns; columnSpan.Maximum = editorColumns; loading = false; LoadSelected();
        }

        private void DashboardRowsChanged(object sender, EventArgs e)
        {
            if (loading) return; int value = (int)dashboardRows.Value; foreach (DashboardItem item in CurrentItems()) if (item.Row + item.RowSpan > value) { placementStatus.Text = "Move components above row " + value + " before reducing the dashboard height."; loading = true; dashboardRows.Value = canvas.Rows; loading = false; return; }
            config.SetDashboardRows(editorColumns, value); DashboardItem selected = canvas.SelectedItem; canvas.SetLayout(CurrentItems(), editorColumns, value); if (selected != null) canvas.SelectItem(selected);
        }

        private void AddClick(object sender, EventArgs e)
        {
            using (AddComponentDialog dialog = new AddComponentDialog(choices))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return; DashboardBoxType type = dialog.SelectedType;
                DashboardItem item = type == DashboardBoxType.Section ? new DashboardItem { BoxType = DashboardBoxType.Section, DisplayName = "NEW SECTION" } : DashboardDefaults.ForSensor(dialog.SelectedSensor == null ? null : dialog.SelectedSensor.Reading, type, editorColumns);
                if (dialog.SelectedSensor != null && dialog.SelectedSensor.Key == "__RAM_USED__") { item.SensorKey = "__RAM_USED__"; item.SensorLabel = "RAM used"; item.SensorName = "System memory"; item.DisplayName = "RAM USED"; item.Thresholds = new double[] { 4.8, 7.2, 9.0, 10.6 }; item.Colors = DashboardItem.AlertColors(); }
                item.ApplyTypeDefaults(editorColumns); int c, r;
                if (!canvas.FindFirstFree(item, out c, out r)) { MessageBox.Show(this, "There is no free space for this component. Move items or increase the dashboard rows.", "Dashboard full", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                item.Column = c; item.Row = r; CurrentItems().Add(item); canvas.SelectItem(item); canvas.Invalidate();
            }
        }

        private void DuplicateClick(object sender, EventArgs e)
        {
            DashboardItem selected = canvas.SelectedItem; if (selected == null) return; DashboardItem copy = selected.Clone(); copy.Id = Guid.NewGuid().ToString("N"); copy.DisplayName += " COPY"; int c, r;
            if (!canvas.FindFirstFree(copy, out c, out r)) { MessageBox.Show(this, "There is no free space for a duplicate.", "Dashboard full", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            copy.Column = c; copy.Row = r; CurrentItems().Add(copy); canvas.SelectItem(copy); canvas.Invalidate();
        }

        private void DeleteClick(object sender, EventArgs e)
        {
            DashboardItem selected = canvas.SelectedItem; if (selected == null) return; CurrentItems().Remove(selected); canvas.SelectItem(null); canvas.Invalidate();
        }

        private void ResetClick(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Reset the " + editorColumns + "-column dashboard to the original layout?", "Reset dashboard", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            List<DashboardItem> replacement = DashboardDefaults.Create(editorColumns); if (editorColumns == 3) config.Dashboard3 = replacement; else config.Dashboard4 = replacement; config.SetDashboardRows(editorColumns, DashboardDefaults.Rows); SwitchLayout(editorColumns);
        }

        private void LoadSelected()
        {
            loading = true; DashboardItem item = canvas.SelectedItem; bool enabled = item != null;
            displayName.Enabled = sensorChoice.Enabled = typeChoice.Enabled = row.Enabled = column.Enabled = columnSpan.Enabled = extrema.Enabled = graphMin.Enabled = graphMax.Enabled = enabled;
            foreach (NumericUpDown threshold in thresholds) if (threshold != null) threshold.Enabled = enabled;
            foreach (Button button in colorButtons) if (button != null) button.Enabled = enabled;
            if (!enabled) { displayName.Text = ""; placementStatus.Text = "Select a component or click + Add."; loading = false; return; }
            displayName.Text = item.DisplayName; typeChoice.SelectedIndex = (int)item.BoxType; row.Value = Clamp(row, item.Row + 1); column.Value = Clamp(column, item.Column + 1); columnSpan.Value = Clamp(columnSpan, item.ColumnSpan); extrema.Checked = item.ShowExtrema;
            graphMin.Value = Clamp(graphMin, (decimal)item.GraphMinimum); graphMax.Value = Clamp(graphMax, (decimal)item.GraphMaximum);
            SelectSensor(item); for (int i = 0; i < 4; i++) thresholds[i].Value = Clamp(thresholds[i], (decimal)item.Thresholds[i]);
            for (int i = 0; i < 5; i++) SetColorButton(i, Color.FromArgb(item.Colors[i]));
            UpdateVisibility(item); placementStatus.Text = ""; loading = false;
        }

        private void SelectSensor(DashboardItem item)
        {
            sensorChoice.SelectedIndex = -1;
            if (item.BoxType == DashboardBoxType.Section) return;
            string key = item.SensorKey; if (key.StartsWith("role:", StringComparison.OrdinalIgnoreCase)) { SensorReading resolved = RoleDefinitions.Resolve(readings, config, key.Substring(5)); if (resolved != null) key = resolved.Key; }
            for (int i = 0; i < sensorChoice.Items.Count; i++) { SensorChoice choice = sensorChoice.Items[i] as SensorChoice; if (choice != null && choice.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) { sensorChoice.SelectedIndex = i; return; } }
            SensorChoice missing = new SensorChoice { Key = item.SensorKey, Label = item.SensorLabel, SensorName = item.SensorName, Display = item.DisplayName + "  —  currently unavailable" }; sensorChoice.Items.Insert(0, missing); sensorChoice.SelectedIndex = 0;
        }

        private void TypeChanged()
        {
            if (loading || canvas.SelectedItem == null) return; DashboardItem item = canvas.SelectedItem; DashboardBoxType old = item.BoxType; int oldSpan = item.ColumnSpan, oldRows = item.RowSpan;
            item.BoxType = (DashboardBoxType)Math.Max(0, typeChoice.SelectedIndex); item.ApplyTypeDefaults(editorColumns);
            if (!canvas.IsPlacementFree(item, item.Column, item.Row, item.ColumnSpan, item.RowSpan))
            {
                int c, r; if (canvas.FindFirstFree(item, out c, out r)) { item.Column = c; item.Row = r; } else { item.BoxType = old; item.ColumnSpan = oldSpan; item.RowSpan = oldRows; placementStatus.Text = "No free space for that box type."; }
            }
            canvas.Invalidate(); LoadSelected();
        }

        private void PositionChanged()
        {
            if (loading || canvas.SelectedItem == null) return; DashboardItem item = canvas.SelectedItem; int c = (int)column.Value - 1, r = (int)row.Value - 1;
            int span = item.BoxType == DashboardBoxType.Graph || item.BoxType == DashboardBoxType.Section ? (int)columnSpan.Value : 1;
            if (!canvas.TryPlace(item, c, r, span, item.RowSpan)) { placementStatus.Text = "That position overlaps another component or exceeds the grid."; LoadSelected(); } else placementStatus.Text = "";
        }

        private void PropertyChanged()
        {
            if (loading || canvas.SelectedItem == null) return; DashboardItem item = canvas.SelectedItem; item.DisplayName = displayName.Text.Trim(); item.ShowExtrema = extrema.Checked;
            item.GraphMinimum = (double)graphMin.Value; item.GraphMaximum = (double)graphMax.Value;
            for (int i = 0; i < 4; i++) item.Thresholds[i] = (double)thresholds[i].Value;
            SensorChoice choice = sensorChoice.SelectedItem as SensorChoice;
            if (choice != null && item.BoxType != DashboardBoxType.Section) { item.SensorKey = choice.Key; item.SensorLabel = choice.Label; item.SensorName = choice.SensorName; }
            canvas.Invalidate();
        }

        private void PickColor(int index)
        {
            DashboardItem item = canvas.SelectedItem; if (item == null) return; using (ColorDialog dialog = new ColorDialog()) { dialog.Color = Color.FromArgb(item.Colors[index]); dialog.FullOpen = true; if (dialog.ShowDialog(this) != DialogResult.OK) return; item.Colors[index] = dialog.Color.ToArgb(); SetColorButton(index, dialog.Color); canvas.Invalidate(); }
        }

        private void UpdateVisibility(DashboardItem item)
        {
            bool graph = item.BoxType == DashboardBoxType.Graph; graphMin.Enabled = graph; graphMax.Enabled = graph; columnSpan.Enabled = graph || item.BoxType == DashboardBoxType.Section; extrema.Enabled = item.BoxType == DashboardBoxType.Big; sensorChoice.Enabled = item.BoxType != DashboardBoxType.Section;
        }

        private List<DashboardItem> CurrentItems() { return editorColumns == 3 ? config.Dashboard3 : config.Dashboard4; }
        private static Button ButtonFor(string text, int x) { Button button = new Button(); button.Text = text; button.Location = new Point(x, 9); button.Size = new Size(text == "Reset layout" ? 108 : 92, 29); return button; }
        private static NumericUpDown Number(decimal minimum, decimal maximum, decimal value) { NumericUpDown control = new NumericUpDown(); control.Minimum = minimum; control.Maximum = maximum; control.DecimalPlaces = minimum < 0 || maximum > 1000 ? 2 : 0; control.Value = Math.Max(minimum, Math.Min(maximum, value)); control.Width = 115; return control; }
        private static decimal Clamp(NumericUpDown control, decimal value) { return Math.Max(control.Minimum, Math.Min(control.Maximum, value)); }
        private static void Heading(TableLayoutPanel table, string text) { Label label = new Label(); label.Text = text; label.AutoSize = true; label.Font = new Font("Segoe UI", 8f, FontStyle.Bold); label.ForeColor = Color.FromArgb(45, 112, 190); label.Margin = new Padding(3, 18, 3, 7); table.Controls.Add(label); table.SetColumnSpan(label, 2); }
        private static void AddRow(TableLayoutPanel table, string name, Control control) { Label label = new Label(); label.Text = name; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; label.Margin = new Padding(3, 6, 8, 6); control.Dock = DockStyle.Fill; control.Margin = new Padding(3, 5, 3, 5); table.Controls.Add(label); table.Controls.Add(control); }
        private void SetColorButton(int index, Color color) { colorButtons[index].BackColor = color; colorButtons[index].ForeColor = color.GetBrightness() < 0.55f ? Color.White : Color.Black; colorButtons[index].FlatAppearance.BorderColor = Color.FromArgb(70, 80, 92); }
    }
}