using System;
using System.Collections.Generic;

namespace VegaDesktopWidget
{
    internal sealed class RoleDefinition
    {
        public string Key, Caption, Group, DefaultDisplayName;
        public int Type;
        public string[] PreferredLabels;
        public RoleDefinition(string key, string caption, string group, int type, string displayName, params string[] labels)
        { Key = key; Caption = caption; Group = group; Type = type; DefaultDisplayName = displayName; PreferredLabels = labels; }
    }

    internal static class RoleDefinitions
    {
        public static readonly RoleDefinition[] All = new RoleDefinition[]
        {
            new RoleDefinition("RamUsage", "RAM used / total", "SYSTEM", -1, "RAM USED"),
            new RoleDefinition("CpuTemp", "CPU temperature", "CPU", 1, "CORE MAX", "Core Max", "CPU Package"),
            new RoleDefinition("CpuLoad", "CPU total load", "CPU", 7, "TOTAL LOAD", "Total CPU Usage", "Total CPU Utility"),
            new RoleDefinition("CpuCore0", "Core 1 frequency", "CPU", 6, "CORE 1", "Core 0 Clock"),
            new RoleDefinition("CpuCore1", "Core 2 frequency", "CPU", 6, "CORE 2", "Core 1 Clock"),
            new RoleDefinition("CpuCore2", "Core 3 frequency", "CPU", 6, "CORE 3", "Core 2 Clock"),
            new RoleDefinition("CpuCore3", "Core 4 frequency", "CPU", 6, "CORE 4", "Core 3 Clock"),
            new RoleDefinition("CpuCore4", "Core 5 frequency", "CPU", 6, "CORE 5", "Core 4 Clock"),
            new RoleDefinition("CpuCore5", "Core 6 frequency", "CPU", 6, "CORE 6", "Core 5 Clock"),
            new RoleDefinition("CpuPower", "CPU power", "CPU", 5, "CPU VRM OUTPUT", "Power (POUT)", "CPU Package Power"),
            new RoleDefinition("GpuTemp", "GPU temperature", "GPU", 1, "TEMPERATURE", "GPU Temperature"),
            new RoleDefinition("GpuHotspot", "GPU hotspot", "GPU", 1, "HOT", "GPU Hot Spot Temperature", "GPU Hotspot Temperature"),
            new RoleDefinition("GpuHbmTemp", "HBM temperature", "GPU", 1, "HBM", "GPU HBM Temperature", "GPU Memory Junction Temperature"),
            new RoleDefinition("GpuLoad", "GPU utilization", "GPU", 7, "UTILIZATION", "GPU Utilization", "GPU D3D Usage"),
            new RoleDefinition("GpuPower", "GPU power estimate", "GPU", 5, "POWER EST.", "GPU ASIC Power", "GPU PPT", "GPU Power"),
            new RoleDefinition("GpuClock", "GPU core clock", "GPU", 6, "CORE CLOCK", "GPU Clock"),
            new RoleDefinition("GpuMemClock", "HBM clock", "GPU", 6, "HBM CLOCK", "GPU Memory Clock"),
            new RoleDefinition("GpuMemory", "VRAM usage", "GPU", 8, "VRAM USED", "GPU Memory Usage", "GPU Memory Allocated"),
            new RoleDefinition("GpuFan", "GPU fan", "GPU", 3, "FAN", "GPU Fan", "GPU Fan1")
        };

        public static SensorReading Resolve(List<SensorReading> readings, WidgetConfig config, string roleKey)
        {
            RoleDefinition d = Find(roleKey); if (d == null || d.Type < 0) return null; string configured;
            if (config.RoleKeys.TryGetValue(roleKey, out configured) && configured.Length > 0)
            { SensorReading mapped = readings.Find(delegate(SensorReading r) { return r.Key.Equals(configured, StringComparison.OrdinalIgnoreCase); }); if (mapped != null) return mapped; }
            foreach (string preferred in d.PreferredLabels)
            { SensorReading exact = readings.Find(delegate(SensorReading r) { return Related(r, d) && (r.Label.Equals(preferred, StringComparison.OrdinalIgnoreCase) || r.OriginalLabel.Equals(preferred, StringComparison.OrdinalIgnoreCase)); }); if (exact != null) return exact; }
            foreach (string preferred in d.PreferredLabels)
            { SensorReading partial = readings.Find(delegate(SensorReading r) { return Related(r, d) && r.Label.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0; }); if (partial != null) return partial; }
            return null;
        }
        public static RoleDefinition Find(string key) { foreach (RoleDefinition d in All) if (d.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) return d; return null; }
        public static bool Related(SensorReading r, RoleDefinition d)
        {
            if (d.Type < 0) return false; string text = (r.SensorName + " " + r.Label).ToLowerInvariant();
            if (d.Group == "GPU") return text.Contains("gpu") || text.Contains("radeon") || text.Contains("vega");
            if (d.Key == "CpuPower" && r.Label.IndexOf("POUT", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return text.Contains("cpu") || text.Contains("xeon") || text.Contains("dts");
        }
    }
}
