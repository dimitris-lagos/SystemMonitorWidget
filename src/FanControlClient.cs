using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace VegaDesktopWidget
{
    internal sealed class FanControlClient : IDisposable
    {
        private readonly object sync = new object();
        private NamedPipeClientStream pipe; private StreamReader reader; private StreamWriter writer; private Process helper;
        private readonly Dictionary<string, int> lastApplied = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> activeControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastFullWrite = DateTime.MinValue;
        private DateTime nextAutomaticRetryUtc = DateTime.MinValue;
        private bool automaticRetryBlocked;
        private string status = "Fan control off";
        public string Status { get { return status; } private set { status = value; } }
        public bool IsConnected { get { return pipe != null && pipe.IsConnected; } }

        public FanScanResult Scan()
        {
            lock (sync)
            {
                automaticRetryBlocked = false; nextAutomaticRetryUtc = DateTime.MinValue;
                try
                {
                    EnsureConnected(); writer.WriteLine("SCAN"); FanScanResult result = ReadScanResult();
                    Status = result.Controls.Count == 0 ? "No controllable fan channels detected" : "Detected " + result.Controls.Count + " controls and " + result.Fans.Count + " RPM sensors";
                    return result;
                }
                catch { CloseConnection(); throw; }
            }
        }

        public List<FanSensorChannel> ReadFanSensors()
        {
            lock (sync)
            {
                if (!IsConnected) return new List<FanSensorChannel>();
                try { writer.WriteLine("READFANS"); return ReadFanLines(); }
                catch { CloseConnection(); throw; }
            }
        }

        public void Update(bool enabled, List<FanProfile> profiles, List<SensorReading> readings)
        {
            lock (sync)
            {
                List<FanProfile> configured = new List<FanProfile>(); if (profiles != null) foreach (FanProfile profile in profiles) if (profile.Enabled) configured.Add(profile);
                if (!enabled || configured.Count == 0) { if (IsConnected) RestoreAllAndStop(); else Status = "Fan control off"; return; }
                if (!IsConnected && (automaticRetryBlocked || DateTime.UtcNow < nextAutomaticRetryUtc)) return;
                try
                {
                    EnsureConnected(); HashSet<string> wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (FanProfile profile in configured)
                    {
                        wanted.Add(profile.ControlId); SensorReading source = ResolveTemperature(profile, readings);
                        int percent = source == null || Double.IsNaN(source.Value) || Double.IsInfinity(source.Value) || source.Value < -20 || source.Value > 130 ? profile.FailSafePercent : profile.OutputFor(source.Value);
                        percent = Math.Max(profile.MinimumPercent, Math.Min(100, percent)); int previous; bool fullRefresh = DateTime.UtcNow - lastFullWrite > TimeSpan.FromSeconds(30);
                        if (!lastApplied.TryGetValue(profile.ControlId, out previous) || previous != percent || fullRefresh)
                        {
                            string response = Request("SET|" + Encode(profile.ControlId) + "|" + percent.ToString(CultureInfo.InvariantCulture)); EnsureOk(response);
                            lastApplied[profile.ControlId] = percent;
                        }
                        activeControls.Add(profile.ControlId);
                    }
                    List<string> restore = new List<string>(); foreach (string id in activeControls) if (!wanted.Contains(id)) restore.Add(id);
                    foreach (string id in restore) { EnsureOk(Request("DEFAULT|" + Encode(id))); activeControls.Remove(id); lastApplied.Remove(id); }
                    if (DateTime.UtcNow - lastFullWrite > TimeSpan.FromSeconds(30)) lastFullWrite = DateTime.UtcNow;
                    Status = "Fan control live · " + configured.Count + " channel" + (configured.Count == 1 ? "" : "s");
                }
                catch (Exception ex) { automaticRetryBlocked = ex.Message.IndexOf("cancelled", StringComparison.OrdinalIgnoreCase) >= 0; nextAutomaticRetryUtc = DateTime.UtcNow.AddMinutes(2); CloseConnection(); Status = "Fan control error · " + ex.Message + (automaticRetryBlocked ? " · open Configure and select OK to retry" : " · retry in 2 min"); }
            }
        }

        public void PrepareConfigurationApply() { lock (sync) { automaticRetryBlocked = false; nextAutomaticRetryUtc = DateTime.MinValue; } }

        public void RestoreAllAndStop()
        {
            lock (sync)
            {
                try { if (IsConnected) { EnsureOk(Request("DEFAULTALL")); Request("EXIT"); } }
                catch { }
                CloseConnection(); activeControls.Clear(); lastApplied.Clear(); Status = "Fan control off · BIOS/default restored";
            }
        }

        private FanScanResult ReadScanResult()
        {
            FanScanResult result = new FanScanResult();
            while (true)
            {
                string line = reader.ReadLine(); if (line == null) throw new IOException("Fan helper disconnected during scan.");
                if (line == "END") break; if (line.StartsWith("ERR|")) throw new InvalidOperationException(DecodeError(line));
                string[] p = line.Split('|'); float value;
                if (p.Length >= 6 && p[0] == "CHANNEL")
                {
                    Single.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                    result.Controls.Add(new FanControlChannel { HardwareId = Decode(p[1]), HardwareName = Decode(p[2]), ControlId = Decode(p[3]), ControlName = Decode(p[4]), CurrentPercent = value });
                }
                else if (p.Length >= 6 && p[0] == "FAN") result.Fans.Add(ParseFan(p));
            }
            return result;
        }

        private List<FanSensorChannel> ReadFanLines()
        {
            List<FanSensorChannel> result = new List<FanSensorChannel>();
            while (true)
            {
                string line = reader.ReadLine(); if (line == null) throw new IOException("Fan helper disconnected while reading RPM sensors.");
                if (line == "END") break; if (line.StartsWith("ERR|")) throw new InvalidOperationException(DecodeError(line));
                string[] p = line.Split('|'); if (p.Length >= 6 && p[0] == "FAN") result.Add(ParseFan(p));
            }
            return result;
        }

        private static FanSensorChannel ParseFan(string[] p)
        {
            float value; Single.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            return new FanSensorChannel { HardwareId = Decode(p[1]), HardwareName = Decode(p[2]), SensorId = Decode(p[3]), SensorName = Decode(p[4]), CurrentRpm = value };
        }

        private void EnsureConnected()
        {
            if (IsConnected) return; CloseConnection(); string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string helperPath = Path.Combine(baseDirectory, "SystemMonitorWidget.FanHelper.exe");
            if (!File.Exists(helperPath)) throw new FileNotFoundException("Fan helper is missing.", helperPath);
            string pipeName = "SystemMonitorWidget.Fan." + Guid.NewGuid().ToString("N"), token = Guid.NewGuid().ToString("N");
            ProcessStartInfo start = new ProcessStartInfo { FileName = helperPath, Arguments = "--pipe " + pipeName + " --token " + token, UseShellExecute = true, Verb = "runas", WorkingDirectory = baseDirectory };
            try { helper = Process.Start(start); } catch (System.ComponentModel.Win32Exception ex) { throw new InvalidOperationException(ex.NativeErrorCode == 1223 ? "Administrator approval was cancelled." : "Could not start elevated fan helper: " + ex.Message); }
            pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None); pipe.Connect(20000);
            reader = new StreamReader(pipe, new UTF8Encoding(false)); writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine("HELLO|" + token); string response = reader.ReadLine(); if (response != "OK") { CloseConnection(); throw new InvalidOperationException("Fan helper authentication failed."); }
        }

        private string Request(string command)
        {
            if (!IsConnected) throw new IOException("Fan helper is not connected."); writer.WriteLine(command); string response = reader.ReadLine();
            if (response == null) throw new IOException("Fan helper disconnected."); return response;
        }
        private static void EnsureOk(string response) { if (response == null || !response.StartsWith("OK")) throw new InvalidOperationException(DecodeError(response)); }
        private static string DecodeError(string response) { if (String.IsNullOrWhiteSpace(response)) return "No response from fan helper."; string[] p = response.Split(new char[] { '|' }, 2); return p.Length == 2 ? Decode(p[1]) : response; }

        private static SensorReading ResolveTemperature(FanProfile profile, List<SensorReading> readings)
        {
            if (readings == null) return null; SensorReading exact = readings.Find(delegate(SensorReading r) { return r.Key.Equals(profile.TemperatureSensorKey, StringComparison.OrdinalIgnoreCase); }); if (exact != null) return exact;
            return readings.Find(delegate(SensorReading r) { bool label = r.Label.Equals(profile.TemperatureSensorLabel, StringComparison.OrdinalIgnoreCase) || r.OriginalLabel.Equals(profile.TemperatureSensorLabel, StringComparison.OrdinalIgnoreCase); return label && (profile.TemperatureSensorName.Length == 0 || r.SensorName.Equals(profile.TemperatureSensorName, StringComparison.OrdinalIgnoreCase)); });
        }
        private static string Encode(string value) { return Uri.EscapeDataString(value ?? ""); }
        private static string Decode(string value) { try { return Uri.UnescapeDataString(value ?? ""); } catch { return value ?? ""; } }

        private void CloseConnection()
        {
            try { if (writer != null) writer.Dispose(); } catch { }
            try { if (reader != null) reader.Dispose(); } catch { }
            try { if (pipe != null) pipe.Dispose(); } catch { }
            writer = null; reader = null; pipe = null; helper = null;
        }
        public void Dispose() { RestoreAllAndStop(); }
    }
}