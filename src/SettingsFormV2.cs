using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal sealed class SettingsForm : Form
    {
        private readonly WidgetConfig working; private readonly List<SensorReading> readings;
        private static readonly int[] ScaleModes = new int[] { 100, 75, 67, 50, 33, 25 };
        private CheckBox topmost, graphs, startup, launchHwinfo;
        private NumericUpDown width, opacity, refresh;
        private TextBox headerTitle;
        private ComboBox uiScale, gridLayout;
        private DashboardEditorControl dashboardEditor;
        public WidgetConfig Result { get { return working; } }

        public SettingsForm(WidgetConfig source, List<SensorReading> available)
        {
            working = Clone(source); readings = available == null ? new List<SensorReading>() : available;
            Text = "System Monitor Widget · Configure"; Font = new Font("Segoe UI", 9f); StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1180, 780); MinimumSize = new Size(1000, 700); BackColor = Color.FromArgb(245, 247, 250); BuildUi();
        }

        private void BuildUi()
        {
            TableLayoutPanel shell = new TableLayoutPanel(); shell.Dock = DockStyle.Fill; shell.ColumnCount = 1; shell.RowCount = 2;
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            TabControl tabs = new TabControl(); tabs.Dock = DockStyle.Fill; tabs.TabPages.Add(BuildGeneralTab()); tabs.TabPages.Add(BuildDashboardTab());
            FlowLayoutPanel footer = new FlowLayoutPanel(); footer.Dock = DockStyle.Fill; footer.FlowDirection = FlowDirection.RightToLeft; footer.WrapContents = false; footer.Padding = new Padding(10, 13, 10, 10); footer.BackColor = Color.FromArgb(245, 247, 250);
            Button cancel = new Button(); cancel.Text = "Cancel"; cancel.DialogResult = DialogResult.Cancel; cancel.Size = new Size(90, 30); cancel.Margin = new Padding(8, 0, 0, 0);
            Button save = new Button(); save.Text = "OK"; save.Size = new Size(100, 30); save.Margin = new Padding(8, 0, 0, 0); save.Click += SaveClick;
            footer.Controls.Add(cancel); footer.Controls.Add(save); shell.Controls.Add(tabs, 0, 0); shell.Controls.Add(footer, 0, 1); Controls.Add(shell); AcceptButton = save; CancelButton = cancel;
        }

        private TabPage BuildGeneralTab()
        {
            TabPage page = new TabPage("Appearance & behavior"); page.BackColor = Color.White; page.Padding = new Padding(24);
            TableLayoutPanel table = new TableLayoutPanel(); table.Dock = DockStyle.Top; table.AutoSize = true; table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddHeading(table, "Desktop behavior");
            headerTitle = AddText(table, "Header title", "Editable title shown on the widget.", working.HeaderTitle, 48);
            topmost = AddCheck(table, "Always on top", "Keep the monitor above normal windows.", working.AlwaysOnTop);
            graphs = AddCheck(table, "Live history graphs", "Draw graph history for graph components.", working.ShowGraphs);
            launchHwinfo = AddCheck(table, "Start HWiNFO if needed", "Use HWiNFO's saved Sensors-only and Auto Start settings.", working.LaunchHWiNFO);
            startup = AddCheck(table, "Start widget with Windows", "Uses your current-user Startup registry entry.", WidgetConfig.IsStartupEnabled());
            AddHeading(table, "Size and refresh");
            gridLayout = AddGridChoice(table, working.GridColumns); uiScale = AddScaleChoice(table, working.UiScaleMode);
            width = AddNumber(table, "Base widget width", "340–600 pixels before scaling", working.Width, 340, 600, 10);
            opacity = AddNumber(table, "Opacity", "65–100 percent", working.OpacityPercent, 65, 100, 1);
            refresh = AddNumber(table, "Refresh interval", "500–5000 milliseconds", working.RefreshMilliseconds, 500, 5000, 100);
            Label note = new Label(); note.Text = "Each grid size has its own independent dashboard. Configure both from the Dashboard editor tab."; note.AutoSize = true; note.ForeColor = Color.FromArgb(75, 84, 96); note.Margin = new Padding(3, 18, 3, 3); table.Controls.Add(note); table.SetColumnSpan(note, 2);
            page.Controls.Add(table); return page;
        }

        private TabPage BuildDashboardTab()
        {
            TabPage page = new TabPage("Dashboard editor"); page.BackColor = Color.White;
            dashboardEditor = new DashboardEditorControl(working, readings); page.Controls.Add(dashboardEditor); return page;
        }

        private void SaveClick(object sender, EventArgs e)
        {
            try
            {
                string error; if (!dashboardEditor.ValidateDashboard(out error)) { MessageBox.Show(this, error, "Dashboard configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                working.AlwaysOnTop = topmost.Checked; working.ShowGraphs = graphs.Checked; working.LaunchHWiNFO = launchHwinfo.Checked;
                working.HeaderTitle = WidgetConfig.NormalizeHeaderTitle(headerTitle.Text);
                working.UiScaleMode = ScaleModes[Math.Max(0, uiScale.SelectedIndex)]; working.GridColumns = gridLayout.SelectedIndex == 0 ? 3 : 4;
                working.Width = (int)width.Value; working.OpacityPercent = (int)opacity.Value; working.RefreshMilliseconds = (int)refresh.Value;
                WidgetConfig.SetStartup(startup.Checked); working.Save(); DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not save settings: " + ex.Message, "System Monitor Widget", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static void AddHeading(TableLayoutPanel table, string text)
        {
            Label heading = new Label(); heading.Text = text.ToUpperInvariant(); heading.AutoSize = true; heading.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            heading.ForeColor = Color.FromArgb(45, 112, 190); heading.Margin = new Padding(3, 18, 3, 7); table.Controls.Add(heading); table.SetColumnSpan(heading, 2);
        }

        private static CheckBox AddCheck(TableLayoutPanel table, string label, string description, bool value)
        {
            Label name = NameLabel(label); CheckBox control = new CheckBox(); control.Text = description; control.Checked = value; control.AutoSize = true; control.Margin = new Padding(3, 8, 3, 8);
            table.Controls.Add(name); table.Controls.Add(control); return control;
        }

        private static TextBox AddText(TableLayoutPanel table, string label, string description, string value, int maximumLength)
        {
            Label name = NameLabel(label); FlowLayoutPanel panel = new FlowLayoutPanel(); panel.AutoSize = true; panel.WrapContents = false;
            TextBox control = new TextBox(); control.Text = value ?? ""; control.MaxLength = maximumLength; control.Width = 285;
            Label hint = new Label(); hint.Text = description; hint.AutoSize = true; hint.ForeColor = Color.Gray; hint.Margin = new Padding(8, 5, 0, 0);
            panel.Controls.Add(control); panel.Controls.Add(hint); table.Controls.Add(name); table.Controls.Add(panel); return control;
        }
        private static NumericUpDown AddNumber(TableLayoutPanel table, string label, string description, int value, int minimum, int maximum, int increment)
        {
            Label name = NameLabel(label); FlowLayoutPanel panel = new FlowLayoutPanel(); panel.AutoSize = true; panel.WrapContents = false;
            NumericUpDown control = new NumericUpDown(); control.Minimum = minimum; control.Maximum = maximum; control.Increment = increment; control.Value = Math.Max(minimum, Math.Min(maximum, value)); control.Width = 90;
            Label hint = new Label(); hint.Text = description; hint.AutoSize = true; hint.ForeColor = Color.Gray; hint.Margin = new Padding(8, 5, 0, 0);
            panel.Controls.Add(control); panel.Controls.Add(hint); table.Controls.Add(name); table.Controls.Add(panel); return control;
        }

        private static ComboBox AddGridChoice(TableLayoutPanel table, int columns)
        {
            Label name = NameLabel("Active grid"); FlowLayoutPanel panel = new FlowLayoutPanel(); panel.AutoSize = true; panel.WrapContents = false;
            ComboBox control = new ComboBox(); control.DropDownStyle = ComboBoxStyle.DropDownList; control.Width = 285; control.Items.AddRange(new object[] { "3 columns", "4 columns" }); control.SelectedIndex = columns == 3 ? 0 : 1;
            Label hint = new Label(); hint.Text = "selects which dashboard is displayed"; hint.AutoSize = true; hint.ForeColor = Color.Gray; hint.Margin = new Padding(8, 5, 0, 0);
            panel.Controls.Add(control); panel.Controls.Add(hint); table.Controls.Add(name); table.Controls.Add(panel); return control;
        }

        private static ComboBox AddScaleChoice(TableLayoutPanel table, int mode)
        {
            Label name = NameLabel("UI scale"); FlowLayoutPanel panel = new FlowLayoutPanel(); panel.AutoSize = true; panel.WrapContents = false;
            ComboBox control = new ComboBox(); control.DropDownStyle = ComboBoxStyle.DropDownList; control.Width = 145; control.Items.AddRange(new object[] { "100% (1/1)", "75% (3/4)", "67% (2/3)", "50% (1/2)", "33% (1/3)", "25% (1/4)" });
            control.SelectedIndex = 0; for (int i = 0; i < ScaleModes.Length; i++) if (ScaleModes[i] == mode) { control.SelectedIndex = i; break; }
            Label hint = new Label(); hint.Text = "scales the complete widget"; hint.AutoSize = true; hint.ForeColor = Color.Gray; hint.Margin = new Padding(8, 5, 0, 0);
            panel.Controls.Add(control); panel.Controls.Add(hint); table.Controls.Add(name); table.Controls.Add(panel); return control;
        }

        private static Label NameLabel(string text) { Label label = new Label(); label.Text = text; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; label.Margin = new Padding(3, 8, 8, 8); return label; }

        private static WidgetConfig Clone(WidgetConfig source)
        {
            WidgetConfig copy = new WidgetConfig(); copy.Left = source.Left; copy.Top = source.Top; copy.Width = source.Width; copy.UiScaleMode = source.UiScaleMode; copy.GridColumns = source.GridColumns; copy.HeaderTitle = source.HeaderTitle;
            copy.RefreshMilliseconds = source.RefreshMilliseconds; copy.OpacityPercent = source.OpacityPercent; copy.ProcessStripMode = source.ProcessStripMode; copy.AlwaysOnTop = source.AlwaysOnTop; copy.ShowGraphs = source.ShowGraphs; copy.LaunchHWiNFO = source.LaunchHWiNFO;
            copy.CpuGraphMin = source.CpuGraphMin; copy.CpuGraphMax = source.CpuGraphMax; copy.GpuGraphMin = source.GpuGraphMin; copy.GpuGraphMax = source.GpuGraphMax;
            copy.DashboardRows3 = source.DashboardRows3; copy.DashboardRows4 = source.DashboardRows4; copy.Dashboard3.Clear(); copy.Dashboard4.Clear();
            foreach (DashboardItem item in source.Dashboard3) copy.Dashboard3.Add(item.Clone()); foreach (DashboardItem item in source.Dashboard4) copy.Dashboard4.Add(item.Clone());
            foreach (KeyValuePair<string, string> pair in source.RoleKeys) copy.RoleKeys[pair.Key] = pair.Value; foreach (KeyValuePair<string, string> pair in source.RoleLabels) copy.RoleLabels[pair.Key] = pair.Value;
            return copy;
        }
    }
}