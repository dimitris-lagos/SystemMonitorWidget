using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal sealed class FanControlSettingsPanel : UserControl
    {
        private sealed class TemperatureChoice
        {
            public SensorReading Reading; public string Key, Label, SensorName, Text;
            public override string ToString() { return Text; }
        }
        private sealed class RpmChoice
        {
            public FanSensorChannel Sensor; public string Text; public bool Live;
            public override string ToString() { return Live && Sensor != null ? Text + "  —  " + Sensor.CurrentRpm.ToString("0") + " RPM" : Text; }
        }
        private sealed class ProfileItem
        {
            public FanProfile Profile; public float? Current;
            public override string ToString() { string name = String.IsNullOrWhiteSpace(Profile.DisplayName) ? Profile.ControlName : Profile.DisplayName; return (Profile.Enabled ? "●  " : "○  ") + name; }
        }

        private readonly WidgetConfig working; private readonly List<SensorReading> readings; private readonly FanControlClient client;
        private readonly List<TemperatureChoice> temperatureChoices = new List<TemperatureChoice>();
        private readonly List<FanSensorChannel> detectedFans = new List<FanSensorChannel>();
        private readonly Timer rpmTimer = new Timer();
        private CheckBox masterEnabled, channelEnabled; private Button scanButton; private Label statusLabel, channelTitle, currentValue;
        private ListBox channelList; private TextBox displayName; private ComboBox temperatureSource, rpmSource; private NumericUpDown minimum, failSafe;
        private readonly NumericUpDown[] temperatures = new NumericUpDown[4], outputs = new NumericUpDown[4]; private FanCurveEditor curve;
        private FanProfile currentProfile; private ProfileItem currentItem; private bool suppress;

        public FanControlSettingsPanel(WidgetConfig config, List<SensorReading> available, FanControlClient fanClient)
        {
            working = config; readings = available ?? new List<SensorReading>(); client = fanClient; Dock = DockStyle.Fill; BackColor = Color.White; Font = new Font("Segoe UI", 9f);
            BuildTemperatureChoices(); BuildUi(); PopulateProfiles(null, null);
            rpmTimer.Interval = 1000; rpmTimer.Tick += RpmTick; rpmTimer.Start(); Disposed += delegate { rpmTimer.Stop(); rpmTimer.Dispose(); };
        }

        private void BuildUi()
        {
            TableLayoutPanel shell = new TableLayoutPanel(); shell.Dock = DockStyle.Fill; shell.RowCount = 2; shell.ColumnCount = 1; shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Panel header = new Panel(); header.Dock = DockStyle.Fill; header.Padding = new Padding(18, 12, 18, 8); header.BackColor = Color.FromArgb(247, 249, 252);
            masterEnabled = new CheckBox(); masterEnabled.Text = "Enable fan control after Apply / OK"; masterEnabled.Checked = working.FanControlEnabled; masterEnabled.AutoSize = true; masterEnabled.Font = new Font("Segoe UI", 10f, FontStyle.Bold); masterEnabled.Location = new Point(18, 14);
            scanButton = new Button(); scanButton.Text = "Scan Super I/O"; scanButton.Size = new Size(142, 31); scanButton.Location = new Point(274, 9); scanButton.Click += ScanClick;
            statusLabel = new Label(); statusLabel.Text = "Scan is read-only. Bind each control to its RPM sensor, then use Apply or OK to start control."; statusLabel.AutoSize = false; statusLabel.Location = new Point(18, 49); statusLabel.Size = new Size(1080, 34); statusLabel.ForeColor = Color.FromArgb(80, 91, 106);
            header.Controls.Add(masterEnabled); header.Controls.Add(scanButton); header.Controls.Add(statusLabel);

            SplitContainer split = new SplitContainer(); split.Dock = DockStyle.Fill; split.FixedPanel = FixedPanel.Panel1; split.SplitterDistance = 300; split.Panel1.Padding = new Padding(14); split.Panel2.Padding = new Padding(10, 14, 14, 14);
            split.SizeChanged += delegate { if (split.Width > 800 && split.SplitterDistance < 280) split.SplitterDistance = 300; };
            Label listTitle = new Label(); listTitle.Text = "DETECTED FAN CHANNELS"; listTitle.Dock = DockStyle.Top; listTitle.Height = 28; listTitle.Font = new Font("Segoe UI", 8f, FontStyle.Bold); listTitle.ForeColor = Color.FromArgb(45, 112, 190);
            channelList = new ListBox(); channelList.Dock = DockStyle.Fill; channelList.BorderStyle = BorderStyle.FixedSingle; channelList.IntegralHeight = false; channelList.DrawMode = DrawMode.OwnerDrawFixed; channelList.ItemHeight = 46; channelList.DrawItem += DrawChannelItem; channelList.SelectedIndexChanged += ChannelSelectionChanged;
            split.Panel1.Controls.Add(channelList); split.Panel1.Controls.Add(listTitle);

            TableLayoutPanel editor = new TableLayoutPanel(); editor.Dock = DockStyle.Fill; editor.ColumnCount = 2; editor.RowCount = 2; editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            channelTitle = new Label(); channelTitle.Text = "Select a detected fan channel"; channelTitle.Dock = DockStyle.Fill; channelTitle.TextAlign = ContentAlignment.MiddleLeft; channelTitle.Font = new Font("Segoe UI", 13f, FontStyle.Bold); channelTitle.ForeColor = Color.FromArgb(33, 43, 56); editor.Controls.Add(channelTitle, 0, 0); editor.SetColumnSpan(channelTitle, 2);
            curve = new FanCurveEditor(); curve.Dock = DockStyle.Fill; curve.Margin = new Padding(0, 0, 14, 0); curve.CurveChanged += CurveChanged; editor.Controls.Add(curve, 0, 1);
            Panel optionsHost = new Panel(); optionsHost.Dock = DockStyle.Fill; optionsHost.AutoScroll = true; editor.Controls.Add(optionsHost, 1, 1); optionsHost.Controls.Add(BuildOptions()); split.Panel2.Controls.Add(editor);
            shell.Controls.Add(header, 0, 0); shell.Controls.Add(split, 0, 1); Controls.Add(shell);
        }

        private Control BuildOptions()
        {
            TableLayoutPanel table = new TableLayoutPanel(); table.Dock = DockStyle.Top; table.AutoSize = true; table.ColumnCount = 2; table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            channelEnabled = AddCheck(table, "Channel", "Use this curve"); channelEnabled.CheckedChanged += delegate { if (!suppress) { SaveCurrent(); RefreshList(); } };
            displayName = AddText(table, "Name"); displayName.TextChanged += delegate { if (!suppress) { SaveCurrent(); RefreshList(); } };
            temperatureSource = AddCombo(table, "Temperature"); temperatureSource.SelectedIndexChanged += delegate { if (!suppress) SaveCurrent(); };
            rpmSource = AddCombo(table, "RPM reading"); rpmSource.SelectedIndexChanged += delegate { if (!suppress) { SaveCurrent(); RefreshCurrentValue(); RefreshList(); } };
            currentValue = new Label(); currentValue.Text = "Control: — · RPM: —"; currentValue.AutoSize = true; currentValue.Font = new Font("Segoe UI", 9f, FontStyle.Bold); currentValue.ForeColor = Color.FromArgb(57, 148, 104); currentValue.Margin = new Padding(3, 2, 3, 4); table.Controls.Add(new Label()); table.Controls.Add(currentValue);
            minimum = AddNumber(table, "Minimum", 20, 100, 1); minimum.ValueChanged += delegate { if (!suppress) SaveCurrent(); };
            failSafe = AddNumber(table, "Fail-safe", 20, 100, 1); failSafe.ValueChanged += delegate { if (!suppress) SaveCurrent(); };
            Label pointsHeading = new Label(); pointsHeading.Text = "CURVE POINTS"; pointsHeading.AutoSize = true; pointsHeading.Font = new Font("Segoe UI", 8f, FontStyle.Bold); pointsHeading.ForeColor = Color.FromArgb(45, 112, 190); pointsHeading.Margin = new Padding(3, 16, 3, 6); table.Controls.Add(pointsHeading); table.SetColumnSpan(pointsHeading, 2);
            TableLayoutPanel pointTable = new TableLayoutPanel(); pointTable.AutoSize = true; pointTable.ColumnCount = 3; pointTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44)); pointTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74)); pointTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            pointTable.Controls.Add(Header("Point"), 0, 0); pointTable.Controls.Add(Header("Temp °C"), 1, 0); pointTable.Controls.Add(Header("Fan %"), 2, 0);
            for (int i = 0; i < 4; i++)
            {
                Label index = new Label(); index.Text = (i + 1).ToString(); index.Dock = DockStyle.Fill; index.TextAlign = ContentAlignment.MiddleCenter; pointTable.Controls.Add(index, 0, i + 1);
                temperatures[i] = CompactNumber(20, 100); outputs[i] = CompactNumber(20, 100); temperatures[i].ValueChanged += NumbersChanged; outputs[i].ValueChanged += NumbersChanged; pointTable.Controls.Add(temperatures[i], 1, i + 1); pointTable.Controls.Add(outputs[i], 2, i + 1);
            }
            table.Controls.Add(new Label()); table.Controls.Add(pointTable);

            Label safety = new Label(); safety.Text = "Close Open Hardware Monitor before enabling control. If the HWiNFO temperature disappears, the fail-safe output is used."; safety.AutoSize = true; safety.MaximumSize = new Size(310, 0); safety.ForeColor = Color.FromArgb(120, 74, 35); safety.Margin = new Padding(3, 12, 3, 8); table.Controls.Add(safety); table.SetColumnSpan(safety, 2);
            SetEditorEnabled(false); return table;
        }

        private void ScanClick(object sender, EventArgs e)
        {
            SaveCurrent(); scanButton.Enabled = false; statusLabel.Text = "Scanning Super I/O controls and RPM sensors…"; Application.DoEvents();
            try
            {
                FanScanResult found = client.Scan(); PopulateProfiles(found.Controls, found.Fans);
                statusLabel.Text = found.Controls.Count + " controls and " + found.Fans.Count + " RPM sensors detected. Scan made no fan changes.";
            }
            catch (Exception ex) { statusLabel.Text = "Scan failed: " + ex.Message; MessageBox.Show(this, ex.Message, "Fan Control scan", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { scanButton.Enabled = true; }
        }

        private void PopulateProfiles(List<FanControlChannel> found, List<FanSensorChannel> fans)
        {
            SaveCurrent(); if (fans != null) { detectedFans.Clear(); detectedFans.AddRange(fans); }
            Dictionary<string, float> current = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (found != null) foreach (FanControlChannel channel in found)
            {
                current[channel.ControlId] = channel.CurrentPercent; FanProfile profile = working.FanProfiles.Find(delegate(FanProfile p) { return p.ControlId.Equals(channel.ControlId, StringComparison.OrdinalIgnoreCase); });
                if (profile == null) { profile = new FanProfile { HardwareId = channel.HardwareId, HardwareName = channel.HardwareName, ControlId = channel.ControlId, ControlName = channel.ControlName, DisplayName = channel.ControlName }; working.FanProfiles.Add(profile); }
                else { profile.HardwareId = channel.HardwareId; profile.HardwareName = channel.HardwareName; profile.ControlName = channel.ControlName; if (String.IsNullOrWhiteSpace(profile.DisplayName)) profile.DisplayName = channel.ControlName; }
                if (String.IsNullOrWhiteSpace(profile.RpmSensorId))
                {
                    string index = LastSegment(channel.ControlId); FanSensorChannel match = detectedFans.Find(delegate(FanSensorChannel fan) { return fan.HardwareId.Equals(channel.HardwareId, StringComparison.OrdinalIgnoreCase) && LastSegment(fan.SensorId) == index; });
                    if (match != null) { profile.RpmSensorId = match.SensorId; profile.RpmSensorName = match.SensorName; }
                }
            }
            channelList.BeginUpdate(); channelList.Items.Clear(); foreach (FanProfile profile in working.FanProfiles) { float value; channelList.Items.Add(new ProfileItem { Profile = profile, Current = current.TryGetValue(profile.ControlId, out value) ? (float?)value : null }); } channelList.EndUpdate();
            if (channelList.Items.Count > 0) channelList.SelectedIndex = 0; else { currentProfile = null; currentItem = null; SetEditorEnabled(false); }
        }

        private void ChannelSelectionChanged(object sender, EventArgs e)
        {
            SaveCurrent(); currentItem = channelList.SelectedItem as ProfileItem; currentProfile = currentItem == null ? null : currentItem.Profile; LoadCurrent();
        }
        private void LoadCurrent()
        {
            suppress = true; try
            {
                if (currentProfile == null) { SetEditorEnabled(false); return; } SetEditorEnabled(true); channelTitle.Text = (String.IsNullOrWhiteSpace(currentProfile.DisplayName) ? currentProfile.ControlName : currentProfile.DisplayName) + "  ·  " + currentProfile.HardwareName;
                channelEnabled.Checked = currentProfile.Enabled; displayName.Text = currentProfile.DisplayName; minimum.Value = Clamp(currentProfile.MinimumPercent, minimum); failSafe.Value = Clamp(currentProfile.FailSafePercent, failSafe);
                PopulateTemperatureCombo(currentProfile); PopulateRpmCombo(currentProfile); List<FanCurvePoint> points = currentProfile.NormalizedPoints(); for (int i = 0; i < 4; i++) { temperatures[i].Value = Clamp(points[i].Temperature, temperatures[i]); outputs[i].Value = Clamp(points[i].Percent, outputs[i]); }
                curve.Points = points; RefreshCurrentValue();
            }
            finally { suppress = false; }
        }
        private void SaveCurrent()
        {
            if (currentProfile == null || suppress) return; currentProfile.Enabled = channelEnabled.Checked; currentProfile.DisplayName = displayName.Text.Trim(); currentProfile.MinimumPercent = (int)minimum.Value; currentProfile.FailSafePercent = Math.Max(currentProfile.MinimumPercent, (int)failSafe.Value);
            TemperatureChoice choice = temperatureSource.SelectedItem as TemperatureChoice; if (choice != null) { currentProfile.TemperatureSensorKey = choice.Key; currentProfile.TemperatureSensorLabel = choice.Label; currentProfile.TemperatureSensorName = choice.SensorName; }
            RpmChoice rpm = rpmSource.SelectedItem as RpmChoice; if (rpm != null && rpm.Sensor != null) { currentProfile.RpmSensorId = rpm.Sensor.SensorId; currentProfile.RpmSensorName = rpm.Sensor.SensorName; } else { currentProfile.RpmSensorId = ""; currentProfile.RpmSensorName = ""; }
            currentProfile.Points = CurveFromNumbers();
        }
        private List<FanCurvePoint> CurveFromNumbers() { List<FanCurvePoint> result = new List<FanCurvePoint>(); for (int i = 0; i < 4; i++) result.Add(new FanCurvePoint { Temperature = (int)temperatures[i].Value, Percent = (int)outputs[i].Value }); return result; }
        private void NumbersChanged(object sender, EventArgs e)
        {
            if (suppress || currentProfile == null) return; suppress = true; try { List<FanCurvePoint> p = new FanProfile { Points = CurveFromNumbers() }.NormalizedPoints(); for (int i = 1; i < 4; i++) if (p[i].Percent < p[i - 1].Percent) p[i].Percent = p[i - 1].Percent; for (int i = 0; i < 4; i++) { temperatures[i].Value = Clamp(p[i].Temperature, temperatures[i]); outputs[i].Value = Clamp(p[i].Percent, outputs[i]); } curve.Points = p; currentProfile.Points = p; } finally { suppress = false; }
        }
        private void CurveChanged(object sender, EventArgs e)
        {
            if (suppress || currentProfile == null) return; suppress = true; try { List<FanCurvePoint> p = curve.Points; for (int i = 0; i < 4; i++) { temperatures[i].Value = Clamp(p[i].Temperature, temperatures[i]); outputs[i].Value = Clamp(p[i].Percent, outputs[i]); } currentProfile.Points = p; } finally { suppress = false; }
        }

        private void RpmTick(object sender, EventArgs e)
        {
            if (!client.IsConnected || detectedFans.Count == 0) return;
            try
            {
                List<FanSensorChannel> refreshed = client.ReadFanSensors(); foreach (FanSensorChannel reading in refreshed)
                {
                    FanSensorChannel existing = detectedFans.Find(delegate(FanSensorChannel fan) { return fan.SensorId.Equals(reading.SensorId, StringComparison.OrdinalIgnoreCase); }); if (existing != null) existing.CurrentRpm = reading.CurrentRpm;
                }
                rpmSource.Refresh(); RefreshCurrentValue();
            }
            catch (Exception ex) { statusLabel.Text = "RPM refresh paused: " + ex.Message; }
        }
        private void RefreshCurrentValue()
        {
            if (currentValue == null || currentProfile == null) return; string control = currentItem != null && currentItem.Current.HasValue ? currentItem.Current.Value.ToString("0.#") + "%" : "not scanned";
            FanSensorChannel fan = detectedFans.Find(delegate(FanSensorChannel item) { return item.SensorId.Equals(currentProfile.RpmSensorId, StringComparison.OrdinalIgnoreCase); });
            string rpm = fan != null ? fan.CurrentRpm.ToString("0") + " RPM" : (String.IsNullOrWhiteSpace(currentProfile.RpmSensorId) ? "not linked" : "unavailable"); currentValue.Text = "Control: " + control + "  ·  " + rpm;
        }

        public bool ValidateAndApply(out string error)
        {
            SaveCurrent(); working.FanControlEnabled = masterEnabled.Checked;
            foreach (FanProfile profile in working.FanProfiles)
            {
                if (!profile.Enabled) continue; if (String.IsNullOrWhiteSpace(profile.TemperatureSensorKey)) { error = "Choose an HWiNFO temperature sensor for " + profile.ControlName + "."; return false; }
                List<FanCurvePoint> p = profile.NormalizedPoints(); for (int i = 1; i < 4; i++) { if (p[i].Temperature <= p[i - 1].Temperature) { error = "Fan-curve temperatures must increase for " + profile.ControlName + "."; return false; } if (p[i].Percent < p[i - 1].Percent) { error = "Fan output must not decrease as temperature rises for " + profile.ControlName + "."; return false; } }
            }
            error = null; return true;
        }

        private void BuildTemperatureChoices()
        {
            foreach (SensorReading reading in readings) if (reading.Type == 1 || (reading.Unit ?? "").IndexOf("C", StringComparison.OrdinalIgnoreCase) >= 0 || (reading.Unit ?? "").Contains("°"))
                temperatureChoices.Add(new TemperatureChoice { Reading = reading, Key = reading.Key, Label = reading.OriginalLabel, SensorName = reading.SensorName, Text = reading.Label + "  —  " + reading.SensorName });
            temperatureChoices.Sort(delegate(TemperatureChoice a, TemperatureChoice b) { return String.Compare(a.Text, b.Text, StringComparison.CurrentCultureIgnoreCase); });
        }
        private void PopulateTemperatureCombo(FanProfile profile)
        {
            temperatureSource.BeginUpdate(); temperatureSource.Items.Clear(); foreach (TemperatureChoice choice in temperatureChoices) temperatureSource.Items.Add(choice);
            int selected = -1; for (int i = 0; i < temperatureSource.Items.Count; i++) if (((TemperatureChoice)temperatureSource.Items[i]).Key.Equals(profile.TemperatureSensorKey, StringComparison.OrdinalIgnoreCase)) { selected = i; break; }
            if (selected < 0 && profile.TemperatureSensorKey.Length > 0) { temperatureSource.Items.Insert(0, new TemperatureChoice { Key = profile.TemperatureSensorKey, Label = profile.TemperatureSensorLabel, SensorName = profile.TemperatureSensorName, Text = profile.TemperatureSensorLabel + "  —  " + profile.TemperatureSensorName + " (not currently available)" }); selected = 0; }
            temperatureSource.SelectedIndex = selected; temperatureSource.EndUpdate();
        }
        private void PopulateRpmCombo(FanProfile profile)
        {
            rpmSource.BeginUpdate(); rpmSource.Items.Clear(); rpmSource.Items.Add(new RpmChoice { Text = "Not assigned" }); int selected = 0;
            foreach (FanSensorChannel sensor in detectedFans) { rpmSource.Items.Add(new RpmChoice { Sensor = sensor, Text = sensor.SensorName, Live = true }); if (sensor.SensorId.Equals(profile.RpmSensorId, StringComparison.OrdinalIgnoreCase)) selected = rpmSource.Items.Count - 1; }
            if (selected == 0 && !String.IsNullOrWhiteSpace(profile.RpmSensorId)) { rpmSource.Items.Add(new RpmChoice { Sensor = new FanSensorChannel { SensorId = profile.RpmSensorId, SensorName = profile.RpmSensorName }, Text = profile.RpmSensorName + " (not currently available)" }); selected = rpmSource.Items.Count - 1; }
            rpmSource.SelectedIndex = selected; rpmSource.EndUpdate();
        }
        private void RefreshList() { int selected = channelList.SelectedIndex; channelList.Refresh(); if (selected >= 0) channelList.SelectedIndex = selected; }
        private void DrawChannelItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground(); if (e.Index < 0 || e.Index >= channelList.Items.Count) return; ProfileItem item = (ProfileItem)channelList.Items[e.Index]; Color primary = (e.State & DrawItemState.Selected) != 0 ? Color.White : Color.FromArgb(35, 47, 61); Color secondary = (e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(225, 237, 250) : Color.FromArgb(105, 118, 134);
            string name = String.IsNullOrWhiteSpace(item.Profile.DisplayName) ? item.Profile.ControlName : item.Profile.DisplayName; string subText = item.Profile.HardwareName; if (!String.IsNullOrWhiteSpace(item.Profile.RpmSensorName)) subText += "  ·  " + item.Profile.RpmSensorName;
            using (Font main = new Font("Segoe UI", 9f, FontStyle.Bold)) using (Font sub = new Font("Segoe UI", 8f)) using (SolidBrush a = new SolidBrush(primary)) using (SolidBrush b = new SolidBrush(secondary)) { e.Graphics.DrawString((item.Profile.Enabled ? "●  " : "○  ") + name, main, a, e.Bounds.X + 6, e.Bounds.Y + 4); e.Graphics.DrawString(subText, sub, b, e.Bounds.X + 25, e.Bounds.Y + 24); } e.DrawFocusRectangle();
        }
        private void SetEditorEnabled(bool enabled) { if (curve != null) curve.Enabled = enabled; if (channelEnabled != null) { channelEnabled.Enabled = enabled; displayName.Enabled = enabled; temperatureSource.Enabled = enabled; rpmSource.Enabled = enabled; minimum.Enabled = enabled; failSafe.Enabled = enabled; for (int i = 0; i < 4; i++) { temperatures[i].Enabled = enabled; outputs[i].Enabled = enabled; } } }

        private static CheckBox AddCheck(TableLayoutPanel table, string label, string text) { Label name = NameLabel(label); CheckBox control = new CheckBox(); control.Text = text; control.AutoSize = true; control.Margin = new Padding(3, 7, 3, 7); table.Controls.Add(name); table.Controls.Add(control); return control; }
        private static TextBox AddText(TableLayoutPanel table, string label) { Label name = NameLabel(label); TextBox control = new TextBox(); control.Dock = DockStyle.Top; table.Controls.Add(name); table.Controls.Add(control); return control; }
        private static ComboBox AddCombo(TableLayoutPanel table, string label) { Label name = NameLabel(label); ComboBox control = new ComboBox(); control.DropDownStyle = ComboBoxStyle.DropDownList; control.Dock = DockStyle.Top; control.DropDownWidth = 520; table.Controls.Add(name); table.Controls.Add(control); return control; }
        private static NumericUpDown AddNumber(TableLayoutPanel table, string label, int min, int max, int increment) { Label name = NameLabel(label); NumericUpDown control = CompactNumber(min, max); control.Increment = increment; table.Controls.Add(name); table.Controls.Add(control); return control; }
        private static NumericUpDown CompactNumber(int min, int max) { NumericUpDown control = new NumericUpDown(); control.Minimum = min; control.Maximum = max; control.Width = 72; control.Margin = new Padding(3, 2, 3, 2); return control; }
        private static Label NameLabel(string text) { Label label = new Label(); label.Text = text; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; label.Margin = new Padding(3, 7, 8, 7); return label; }
        private static Label Header(string text) { Label label = new Label(); label.Text = text; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleCenter; label.Font = new Font("Segoe UI", 8f, FontStyle.Bold); label.ForeColor = Color.Gray; return label; }
        private static decimal Clamp(int value, NumericUpDown control) { return Math.Max(control.Minimum, Math.Min(control.Maximum, value)); }
        private static string LastSegment(string id) { if (String.IsNullOrWhiteSpace(id)) return ""; int slash = id.LastIndexOf('/'); return slash >= 0 ? id.Substring(slash + 1) : id; }
    }
}