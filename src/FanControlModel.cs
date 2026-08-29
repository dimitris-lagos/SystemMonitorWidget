using System;
using System.Collections.Generic;
using System.Globalization;

namespace VegaDesktopWidget
{
    internal sealed class FanCurvePoint
    {
        public int Temperature;
        public int Percent;
        public FanCurvePoint Clone() { return new FanCurvePoint { Temperature = Temperature, Percent = Percent }; }
    }

    internal sealed class FanProfile
    {
        public string HardwareId = "", HardwareName = "", ControlId = "", ControlName = "", DisplayName = "";
        public string TemperatureSensorKey = "", TemperatureSensorLabel = "", TemperatureSensorName = "";
        public string RpmSensorId = "", RpmSensorName = "";
        public bool Enabled;
        public int MinimumPercent = 30, FailSafePercent = 100;
        public List<FanCurvePoint> Points = DefaultPoints();

        public static List<FanCurvePoint> DefaultPoints()
        {
            return new List<FanCurvePoint> {
                new FanCurvePoint { Temperature = 35, Percent = 35 },
                new FanCurvePoint { Temperature = 50, Percent = 50 },
                new FanCurvePoint { Temperature = 65, Percent = 75 },
                new FanCurvePoint { Temperature = 80, Percent = 100 }
            };
        }

        public int OutputFor(double temperature)
        {
            List<FanCurvePoint> p = NormalizedPoints(); double output;
            if (temperature <= p[0].Temperature) output = p[0].Percent;
            else if (temperature >= p[3].Temperature) output = p[3].Percent;
            else
            {
                int upper = 1; while (upper < p.Count && temperature > p[upper].Temperature) upper++;
                FanCurvePoint a = p[upper - 1], b = p[upper]; double span = Math.Max(1, b.Temperature - a.Temperature);
                output = a.Percent + (temperature - a.Temperature) * (b.Percent - a.Percent) / span;
            }
            return Math.Max(MinimumPercent, Math.Min(100, (int)Math.Round(output, MidpointRounding.AwayFromZero)));
        }

        public List<FanCurvePoint> NormalizedPoints()
        {
            List<FanCurvePoint> result = new List<FanCurvePoint>();
            foreach (FanCurvePoint point in Points ?? new List<FanCurvePoint>()) result.Add(point.Clone());
            while (result.Count < 4) result.Add(DefaultPoints()[result.Count]);
            if (result.Count > 4) result.RemoveRange(4, result.Count - 4);
            result.Sort(delegate(FanCurvePoint a, FanCurvePoint b) { return a.Temperature.CompareTo(b.Temperature); }); return result;
        }

        public FanProfile Clone()
        {
            FanProfile copy = (FanProfile)MemberwiseClone(); copy.Points = new List<FanCurvePoint>();
            foreach (FanCurvePoint point in Points) copy.Points.Add(point.Clone()); return copy;
        }

        public string Serialize()
        {
            List<FanCurvePoint> p = NormalizedPoints(); string[] pointValues = new string[4];
            for (int i = 0; i < 4; i++) pointValues[i] = p[i].Temperature + ":" + p[i].Percent;
            return String.Join("|", new string[] {
                Escape(HardwareId), Escape(HardwareName), Escape(ControlId), Escape(ControlName), Escape(DisplayName), Enabled.ToString(),
                Escape(TemperatureSensorKey), Escape(TemperatureSensorLabel), Escape(TemperatureSensorName),
                MinimumPercent.ToString(CultureInfo.InvariantCulture), FailSafePercent.ToString(CultureInfo.InvariantCulture), String.Join(",", pointValues),
                Escape(RpmSensorId), Escape(RpmSensorName)
            });
        }

        public static FanProfile Deserialize(string value)
        {
            string[] p = (value ?? "").Split('|'); if (p.Length < 12) return null;
            FanProfile profile = new FanProfile(); bool enabled; int minimum, failSafe;
            profile.HardwareId = Unescape(p[0]); profile.HardwareName = Unescape(p[1]); profile.ControlId = Unescape(p[2]); profile.ControlName = Unescape(p[3]); profile.DisplayName = Unescape(p[4]);
            profile.Enabled = Boolean.TryParse(p[5], out enabled) && enabled;
            profile.TemperatureSensorKey = Unescape(p[6]); profile.TemperatureSensorLabel = Unescape(p[7]); profile.TemperatureSensorName = Unescape(p[8]);
            profile.MinimumPercent = Int32.TryParse(p[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out minimum) ? Math.Max(20, Math.Min(100, minimum)) : 30;
            profile.FailSafePercent = Int32.TryParse(p[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out failSafe) ? Math.Max(profile.MinimumPercent, Math.Min(100, failSafe)) : 100;
            string[] points = p[11].Split(','); profile.Points.Clear();
            foreach (string raw in points)
            {
                string[] pair = raw.Split(':'); int temperature, percent;
                if (pair.Length == 2 && Int32.TryParse(pair[0], out temperature) && Int32.TryParse(pair[1], out percent))
                    profile.Points.Add(new FanCurvePoint { Temperature = Math.Max(0, Math.Min(120, temperature)), Percent = Math.Max(0, Math.Min(100, percent)) });
            }
            if (profile.Points.Count != 4) profile.Points = DefaultPoints(); else profile.Points = profile.NormalizedPoints();
            if (p.Length > 12) profile.RpmSensorId = Unescape(p[12]);
            if (p.Length > 13) profile.RpmSensorName = Unescape(p[13]);
            return profile.ControlId.Length == 0 ? null : profile;
        }

        private static string Escape(string value) { return Uri.EscapeDataString(value ?? ""); }
        private static string Unescape(string value) { try { return Uri.UnescapeDataString(value ?? ""); } catch { return value ?? ""; } }
    }

    internal sealed class FanControlChannel
    {
        public string HardwareId = "", HardwareName = "", ControlId = "", ControlName = "";
        public float CurrentPercent;
        public override string ToString() { return ControlName + "  —  " + HardwareName; }
    }

    internal sealed class FanSensorChannel
    {
        public string HardwareId = "", HardwareName = "", SensorId = "", SensorName = "";
        public float CurrentRpm;
        public override string ToString() { return SensorName + "  —  " + HardwareName; }
    }

    internal sealed class FanScanResult
    {
        public readonly List<FanControlChannel> Controls = new List<FanControlChannel>();
        public readonly List<FanSensorChannel> Fans = new List<FanSensorChannel>();
    }
}