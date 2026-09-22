using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VegaDesktopWidget.HWiNFORestartHelper
{
    internal static class HWiNFORestartHelperProgram
    {
        private const uint FileMapRead = 0x0004, SignatureActive = 0x53695748;
        private const string MapName = "Global\\HWiNFO_SENS_SM2";
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr OpenFileMapping(uint access, bool inheritHandle, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr MapViewOfFile(IntPtr mapping, uint access, uint offsetHigh, uint offsetLow, UIntPtr bytesToMap);
        [DllImport("kernel32.dll")] private static extern bool UnmapViewOfFile(IntPtr address);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);

        [STAThread]
        private static int Main(string[] args)
        {
            string executable = Argument(args, "--executable"), resultPath = Argument(args, "--result"), logPath = Argument(args, "--log");
            if (resultPath.Length == 0 || logPath.Length == 0) return 2;
            try
            {
                Log(logPath, "Elevated restart helper started.");
                ValidateExecutable(executable);
                List<int> previousIds = StopHWiNFO(logPath);
                Process started = StartHWiNFO(executable, logPath);
                int startedId = started.Id; started.Dispose();
                WaitForHealthyRestart(startedId, previousIds, logPath);
                WriteResult(resultPath, "OK|" + startedId);
                Log(logPath, "Restart verified. New process ID " + startedId + " and shared memory are available.");
                return 0;
            }
            catch (Exception ex)
            {
                Log(logPath, "Restart helper failed." + Environment.NewLine + ex);
                try { WriteResult(resultPath, "ERROR|" + ex.Message.Replace("\r", " ").Replace("\n", " ")); } catch { }
                return 1;
            }
        }

        private static void ValidateExecutable(string executable)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("HWiNFO executable was not found.", executable);
            string name = Path.GetFileName(executable);
            if (!name.Equals("HWiNFO64.exe", StringComparison.OrdinalIgnoreCase) && !name.Equals("HWiNFO32.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The configured executable is not HWiNFO32.exe or HWiNFO64.exe.");
        }

        private static List<int> StopHWiNFO(string logPath)
        {
            List<int> ids = new List<int>(); Process[] processes = GetHWiNFOProcesses();
            try
            {
                foreach (Process process in processes)
                {
                    ids.Add(process.Id); Log(logPath, "Stopping " + process.ProcessName + " process ID " + process.Id + ".");
                    if (process.HasExited) continue;
                    bool closing = false; try { closing = process.CloseMainWindow(); } catch (Exception ex) { Log(logPath, "CloseMainWindow failed for process ID " + process.Id + "." + Environment.NewLine + ex); }
                    if (closing) try { process.WaitForExit(10000); } catch (Exception ex) { Log(logPath, "Waiting for graceful exit failed for process ID " + process.Id + "." + Environment.NewLine + ex); }
                    if (!process.HasExited)
                    {
                        Log(logPath, "Terminating process ID " + process.Id + "."); process.Kill();
                        if (!process.WaitForExit(10000)) throw new InvalidOperationException("HWiNFO process " + process.Id + " did not terminate.");
                    }
                }
            }
            finally { foreach (Process process in processes) process.Dispose(); }
            Thread.Sleep(750);
            Process[] remaining = GetHWiNFOProcesses();
            try { if (remaining.Length > 0) throw new InvalidOperationException("One or more HWiNFO processes are still running."); }
            finally { foreach (Process process in remaining) process.Dispose(); }
            return ids;
        }

        private static Process StartHWiNFO(string executable, string logPath)
        {
            ProcessStartInfo start = new ProcessStartInfo(); start.FileName = executable; start.WorkingDirectory = Path.GetDirectoryName(executable); start.UseShellExecute = true;
            Process process = Process.Start(start); if (process == null) throw new InvalidOperationException("Windows did not return the new HWiNFO process.");
            Log(logPath, "Started " + Path.GetFileName(executable) + " with process ID " + process.Id + "."); return process;
        }

        private static void WaitForHealthyRestart(int startedId, List<int> previousIds, string logPath)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(45); bool processSeen = false;
            while (DateTime.UtcNow < deadline)
            {
                Process[] processes = GetHWiNFOProcesses();
                try { foreach (Process process in processes) if (process.Id == startedId && !previousIds.Contains(process.Id) && !process.HasExited) { processSeen = true; break; } }
                finally { foreach (Process process in processes) process.Dispose(); }
                if (processSeen && IsSharedMemoryActive()) return;
                Thread.Sleep(500);
            }
            if (!processSeen) throw new InvalidOperationException("The new HWiNFO process did not remain running.");
            throw new InvalidOperationException("HWiNFO restarted, but Global\\HWiNFO_SENS_SM2 did not become active within 45 seconds.");
        }

        private static bool IsSharedMemoryActive()
        {
            IntPtr mapping = OpenFileMapping(FileMapRead, false, MapName); if (mapping == IntPtr.Zero) return false;
            IntPtr view = IntPtr.Zero;
            try { view = MapViewOfFile(mapping, FileMapRead, 0, 0, UIntPtr.Zero); return view != IntPtr.Zero && unchecked((uint)Marshal.ReadInt32(view)) == SignatureActive; }
            finally { if (view != IntPtr.Zero) UnmapViewOfFile(view); CloseHandle(mapping); }
        }

        private static Process[] GetHWiNFOProcesses()
        {
            List<Process> result = new List<Process>();
            foreach (string name in new string[] { "HWiNFO64", "HWiNFO32" }) result.AddRange(Process.GetProcessesByName(name));
            return result.ToArray();
        }

        private static void Log(string path, string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [helper] " + message + Environment.NewLine, new UTF8Encoding(false));
        }
        private static void WriteResult(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value, new UTF8Encoding(false)); }
        private static string Argument(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return ""; }
    }
}
