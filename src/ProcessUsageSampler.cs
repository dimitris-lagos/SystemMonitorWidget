using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace VegaDesktopWidget
{
    internal sealed class ProcessUsage
    {
        public string Name;
        public double CpuPercent;
        public long WorkingSetBytes;

        public string CompactText
        {
            get
            {
                double gib = WorkingSetBytes / 1073741824.0;
                string memory = gib >= 1.0
                    ? gib.ToString("0.0", CultureInfo.InvariantCulture) + " GB"
                    : Math.Max(1.0, WorkingSetBytes / 1048576.0).ToString("0", CultureInfo.InvariantCulture) + " MB";
                return Name + " " + CpuPercent.ToString("0", CultureInfo.InvariantCulture) + "% " + memory;
            }
        }
    }

    internal sealed class ProcessUsageSampler
    {
        private sealed class ProcessPoint { public double CpuMilliseconds; }
        private sealed class Aggregate { public string Name; public double CpuPercent; public long WorkingSetBytes; }

        private readonly Dictionary<int, ProcessPoint> previous = new Dictionary<int, ProcessPoint>();
        private List<ProcessUsage> cached = new List<ProcessUsage>();
        private long previousTimestamp;
        private long lastSampleTimestamp;
        private readonly int ownProcessId = Process.GetCurrentProcess().Id;

        public List<ProcessUsage> SampleTopByMemory(int count)
        {
            long now = Stopwatch.GetTimestamp();
            if (lastSampleTimestamp != 0 && SecondsBetween(lastSampleTimestamp, now) < 0.75) return new List<ProcessUsage>(cached);

            double elapsedMilliseconds = previousTimestamp == 0 ? 0.0 : SecondsBetween(previousTimestamp, now) * 1000.0;
            Dictionary<int, ProcessPoint> next = new Dictionary<int, ProcessPoint>();
            Dictionary<string, Aggregate> groups = new Dictionary<string, Aggregate>(StringComparer.OrdinalIgnoreCase);

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    int id = process.Id;
                    if (id == 0 || id == ownProcessId) continue;
                    string name = FriendlyName(process.ProcessName);
                    long memory = process.WorkingSet64;
                    double cpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds;
                    if (memory <= 0 || name.Length == 0) continue;

                    double cpuPercent = 0.0;
                    ProcessPoint old;
                    if (elapsedMilliseconds > 0.0 && previous.TryGetValue(id, out old) && cpuMilliseconds >= old.CpuMilliseconds)
                        cpuPercent = (cpuMilliseconds - old.CpuMilliseconds) * 100.0 / (elapsedMilliseconds * Math.Max(1, Environment.ProcessorCount));
                    cpuPercent = Math.Max(0.0, Math.Min(100.0, cpuPercent));
                    next[id] = new ProcessPoint { CpuMilliseconds = cpuMilliseconds };

                    Aggregate aggregate;
                    if (!groups.TryGetValue(name, out aggregate))
                    {
                        aggregate = new Aggregate { Name = name };
                        groups[name] = aggregate;
                    }
                    aggregate.WorkingSetBytes += memory;
                    aggregate.CpuPercent += cpuPercent;
                }
                catch { }
                finally { process.Dispose(); }
            }

            previous.Clear();
            foreach (KeyValuePair<int, ProcessPoint> pair in next) previous[pair.Key] = pair.Value;
            previousTimestamp = now;
            lastSampleTimestamp = now;

            List<Aggregate> ordered = new List<Aggregate>(groups.Values);
            ordered.Sort(delegate(Aggregate a, Aggregate b) {
                int byMemory = b.WorkingSetBytes.CompareTo(a.WorkingSetBytes);
                return byMemory != 0 ? byMemory : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            });

            List<ProcessUsage> result = new List<ProcessUsage>();
            for (int i = 0; i < ordered.Count && i < Math.Max(0, count); i++)
            {
                Aggregate item = ordered[i];
                result.Add(new ProcessUsage {
                    Name = item.Name,
                    CpuPercent = Math.Min(100.0, item.CpuPercent),
                    WorkingSetBytes = item.WorkingSetBytes
                });
            }
            cached = result;
            return new List<ProcessUsage>(cached);
        }

        private static double SecondsBetween(long first, long second) { return (second - first) / (double)Stopwatch.Frequency; }

        private static string FriendlyName(string processName)
        {
            string lower = (processName ?? "").Trim().ToLowerInvariant();
            if (lower == "opera") return "Opera";
            if (lower == "opera_gx") return "Opera GX";
            if (lower == "chrome") return "Chrome";
            if (lower == "msedge") return "Edge";
            if (lower == "firefox") return "Firefox";
            if (lower == "code") return "VS Code";
            if (lower == "codex") return "Codex";
            if (lower == "chatgpt") return "ChatGPT";
            if (lower == "steam" || lower == "steamwebhelper") return "Steam";
            if (lower == "dwm") return "DWM";
            if (lower == "explorer") return "Explorer";
            if (lower == "hwinfo64") return "HWiNFO";
            if (lower == "radeonsoftware") return "Radeon";
            string cleaned = lower.Replace('_', ' ').Replace('-', ' ').Trim();
            return cleaned.Length == 0 ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned);
        }
    }
}
