using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using OpenHardwareMonitor.Hardware;

namespace VegaDesktopWidget.FanHelper
{
    internal static class FanControlHelperProgram
    {
        private static Computer computer;
        private static readonly Dictionary<string, ISensor> controls = new Dictionary<string, ISensor>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ISensor> fanSensors = new Dictionary<string, ISensor>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> changedControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [STAThread]
        private static int Main(string[] args)
        {
            string pipeName = Argument(args, "--pipe"), token = Argument(args, "--token"); if (pipeName.Length == 0 || token.Length == 0) return 2;
            try
            {
                using (NamedPipeServerStream pipe = CreatePipe(pipeName))
                {
                    IAsyncResult pending = pipe.BeginWaitForConnection(null, null); if (!pending.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30))) return 4; pipe.EndWaitForConnection(pending);
                    using (StreamReader reader = new StreamReader(pipe, new UTF8Encoding(false))) using (StreamWriter writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true })
                    {
                        string hello = reader.ReadLine(); if (hello != "HELLO|" + token) { writer.WriteLine("ERR|Authentication%20failed"); return 3; } writer.WriteLine("OK");
                        while (pipe.IsConnected)
                        {
                            string line = reader.ReadLine(); if (line == null) break;
                            try { if (!Handle(line, writer)) break; } catch (Exception ex) { writer.WriteLine("ERR|" + Encode(ex.Message)); }
                        }
                    }
                }
                return 0;
            }
            catch (Exception ex) { try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemMonitorWidget.FanHelper.error.log"), ex.ToString()); } catch { } return 1; }
            finally { RestoreAll(); CloseComputer(); }
        }

        private static NamedPipeServerStream CreatePipe(string pipeName)
        {
            PipeSecurity security = new PipeSecurity(); SecurityIdentifier user = WindowsIdentity.GetCurrent().User;
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
            return new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security);
        }
        private static bool Handle(string line, StreamWriter writer)
        {
            string[] p = line.Split('|'); string command = p[0].ToUpperInvariant();
            if (command == "SCAN")
            {
                EnsureInitialized(); RefreshHardware(); foreach (ISensor sensor in SortedControls())
                {
                    string hardwareId = sensor.Hardware.Identifier.ToString(), hardwareName = sensor.Hardware.Name, controlId = sensor.Identifier.ToString();
                    float current = sensor.Value ?? sensor.Control.SoftwareValue;
                    writer.WriteLine("CHANNEL|" + Encode(hardwareId) + "|" + Encode(hardwareName) + "|" + Encode(controlId) + "|" + Encode(sensor.Name) + "|" + current.ToString("0.##", CultureInfo.InvariantCulture));
                }
                WriteFans(writer); writer.WriteLine("END"); return true;
            }
            if (command == "READFANS") { EnsureInitialized(); RefreshHardware(); WriteFans(writer); writer.WriteLine("END"); return true; }
            if (command == "SET" && p.Length >= 3)
            {
                EnsureNoOtherController(); EnsureInitialized(); string id = Decode(p[1]); int percent; if (!Int32.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out percent)) throw new InvalidOperationException("Invalid fan percentage.");
                percent = Math.Max(20, Math.Min(100, percent)); ISensor sensor = FindControl(id); sensor.Control.SetSoftware(percent); changedControls.Add(id); writer.WriteLine("OK|" + percent); return true;
            }
            if (command == "DEFAULT" && p.Length >= 2)
            {
                EnsureInitialized(); string id = Decode(p[1]); ISensor sensor = FindControl(id); sensor.Control.SetDefault(); changedControls.Remove(id); writer.WriteLine("OK"); return true;
            }
            if (command == "DEFAULTALL") { RestoreAll(); writer.WriteLine("OK"); return true; }
            if (command == "EXIT") { writer.WriteLine("OK"); return false; }
            throw new InvalidOperationException("Unknown fan-helper command.");
        }

        private static void WriteFans(StreamWriter writer)
        {
            foreach (ISensor sensor in SortedFans())
            {
                string hardwareId = sensor.Hardware.Identifier.ToString(), hardwareName = sensor.Hardware.Name, sensorId = sensor.Identifier.ToString(); float rpm = sensor.Value ?? 0;
                writer.WriteLine("FAN|" + Encode(hardwareId) + "|" + Encode(hardwareName) + "|" + Encode(sensorId) + "|" + Encode(sensor.Name) + "|" + rpm.ToString("0.##", CultureInfo.InvariantCulture));
            }
        }

        private static void EnsureInitialized()
        {
            if (computer != null) return; computer = new Computer { MainboardEnabled = true }; computer.Open(); RefreshHardware(); controls.Clear(); fanSensors.Clear();
            foreach (IHardware hardware in computer.Hardware) AddSensors(hardware);
            if (controls.Count == 0) throw new InvalidOperationException("No controllable Super I/O fan channels were detected.");
        }
        private static void AddSensors(IHardware hardware)
        {
            hardware.Update(); bool isLpc = hardware.Identifier.ToString().StartsWith("/lpc/", StringComparison.OrdinalIgnoreCase);
            if (isLpc) foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.Control != null && sensor.SensorType == SensorType.Control) controls[sensor.Identifier.ToString()] = sensor;
                if (sensor.SensorType == SensorType.Fan) fanSensors[sensor.Identifier.ToString()] = sensor;
            }
            foreach (IHardware child in hardware.SubHardware) AddSensors(child);
        }
        private static void RefreshHardware() { if (computer == null) return; foreach (IHardware hardware in computer.Hardware) UpdateRecursive(hardware); }
        private static void UpdateRecursive(IHardware hardware) { hardware.Update(); foreach (IHardware child in hardware.SubHardware) UpdateRecursive(child); }
        private static List<ISensor> SortedControls() { List<ISensor> result = new List<ISensor>(controls.Values); result.Sort(delegate(ISensor a, ISensor b) { return String.Compare(a.Identifier.ToString(), b.Identifier.ToString(), StringComparison.OrdinalIgnoreCase); }); return result; }
        private static List<ISensor> SortedFans() { List<ISensor> result = new List<ISensor>(fanSensors.Values); result.Sort(delegate(ISensor a, ISensor b) { return String.Compare(a.Identifier.ToString(), b.Identifier.ToString(), StringComparison.OrdinalIgnoreCase); }); return result; }
        private static ISensor FindControl(string id) { ISensor sensor; if (!controls.TryGetValue(id, out sensor) || sensor.Control == null) throw new InvalidOperationException("Configured fan channel was not detected: " + id); return sensor; }

        private static void RestoreAll()
        {
            if (computer == null) return; foreach (string id in new List<string>(changedControls)) { try { FindControl(id).Control.SetDefault(); } catch { } } changedControls.Clear();
        }
        private static void CloseComputer() { if (computer == null) return; try { computer.Close(); } catch { } computer = null; controls.Clear(); fanSensors.Clear(); }
        private static void EnsureNoOtherController()
        {
            if (Process.GetProcessesByName("OpenHardwareMonitor").Length > 0 || Process.GetProcessesByName("OpenHardwareMonitor_x64").Length > 0)
                throw new InvalidOperationException("Close Open Hardware Monitor before enabling widget fan control.");
        }
        private static string Argument(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return ""; }
        private static string Encode(string value) { return Uri.EscapeDataString(value ?? ""); }
        private static string Decode(string value) { try { return Uri.UnescapeDataString(value ?? ""); } catch { return value ?? ""; } }
    }
}