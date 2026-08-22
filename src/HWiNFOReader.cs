using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace VegaDesktopWidget
{
    internal sealed class SensorReading
    {
        public uint SensorId;
        public uint SensorInstance;
        public uint ReadingId;
        public int Type;
        public string SensorName;
        public string OriginalLabel;
        public string Label;
        public string Unit;
        public double Value;
        public double Minimum;
        public double Maximum;
        public double Average;
        public string Key { get { return SensorId.ToString("X8") + ":" + SensorInstance.ToString("X8") + ":" + ReadingId.ToString("X8"); } }
        public string FullName { get { return Label + "  —  " + SensorName; } }
    }

    internal sealed class HWiNFOReader
    {
        private const uint FileMapRead = 0x0004, Synchronize = 0x00100000, WaitObject0 = 0, WaitAbandoned = 0x80;
        private const uint SignatureActive = 0x53695748;
        private const string MapName = "Global\\HWiNFO_SENS_SM2", MutexName = "Global\\HWiNFO_SM2_MUTEX";
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr OpenFileMapping(uint a, bool b, string n);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr MapViewOfFile(IntPtr h, uint a, uint oh, uint ol, UIntPtr s);
        [DllImport("kernel32.dll")] private static extern bool UnmapViewOfFile(IntPtr p);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr OpenMutex(uint a, bool b, string n);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll")] private static extern bool ReleaseMutex(IntPtr h);

        public List<SensorReading> Read(out string status)
        {
            List<SensorReading> result = new List<SensorReading>();
            status = "Waiting for HWiNFO shared memory";
            IntPtr mapping = OpenFileMapping(FileMapRead, false, MapName);
            if (mapping == IntPtr.Zero) return result;
            IntPtr mutex = OpenMutex(Synchronize, false, MutexName), view = IntPtr.Zero;
            bool ownsMutex = false;
            try
            {
                if (mutex != IntPtr.Zero)
                {
                    uint wait = WaitForSingleObject(mutex, 150);
                    ownsMutex = wait == WaitObject0 || wait == WaitAbandoned;
                    if (!ownsMutex) { status = "HWiNFO is busy"; return result; }
                }
                view = MapViewOfFile(mapping, FileMapRead, 0, 0, UIntPtr.Zero);
                if (view == IntPtr.Zero) { status = "Cannot map HWiNFO data"; return result; }
                if (ReadUInt32(view, 0) != SignatureActive) { status = "HWiNFO sensors inactive"; return result; }
                uint sensorOffset = ReadUInt32(view, 20), sensorSize = ReadUInt32(view, 24), sensorCount = ReadUInt32(view, 28);
                uint readingOffset = ReadUInt32(view, 32), readingSize = ReadUInt32(view, 36), readingCount = ReadUInt32(view, 40);
                if (sensorCount > 4096 || readingCount > 65536 || sensorSize < 264 || readingSize < 316)
                { status = "Unsupported HWiNFO data format"; return result; }
                List<SensorIdentity> sensors = new List<SensorIdentity>();
                uint i;
                for (i = 0; i < sensorCount; i++)
                {
                    long p = (long)sensorOffset + (long)sensorSize * i;
                    SensorIdentity sensor = new SensorIdentity();
                    sensor.Id = ReadUInt32(view, p); sensor.Instance = ReadUInt32(view, p + 4);
                    string original = ReadAnsi(view, p + 8, 128), user = ReadAnsi(view, p + 136, 128);
                    sensor.Name = String.IsNullOrWhiteSpace(user) ? original : user; sensors.Add(sensor);
                }
                for (i = 0; i < readingCount; i++)
                {
                    long p = (long)readingOffset + (long)readingSize * i;
                    uint sensorIndex = ReadUInt32(view, p + 4); if (sensorIndex >= sensors.Count) continue;
                    SensorIdentity sensor = sensors[(int)sensorIndex]; SensorReading reading = new SensorReading();
                    reading.Type = (int)ReadUInt32(view, p); reading.SensorId = sensor.Id; reading.SensorInstance = sensor.Instance;
                    reading.ReadingId = ReadUInt32(view, p + 8); reading.SensorName = sensor.Name;
                    reading.OriginalLabel = ReadAnsi(view, p + 12, 128); string userLabel = ReadAnsi(view, p + 140, 128);
                    reading.Label = String.IsNullOrWhiteSpace(userLabel) ? reading.OriginalLabel : userLabel;
                    reading.Unit = ReadAnsi(view, p + 268, 16); reading.Value = ReadDouble(view, p + 284);
                    reading.Minimum = ReadDouble(view, p + 292); reading.Maximum = ReadDouble(view, p + 300); reading.Average = ReadDouble(view, p + 308);
                    result.Add(reading);
                }
                status = "Live · " + result.Count + " readings"; return result;
            }
            finally
            {
                if (view != IntPtr.Zero) UnmapViewOfFile(view); if (ownsMutex && mutex != IntPtr.Zero) ReleaseMutex(mutex);
                if (mutex != IntPtr.Zero) CloseHandle(mutex); CloseHandle(mapping);
            }
        }

        private static uint ReadUInt32(IntPtr b, long o) { return unchecked((uint)Marshal.ReadInt32(new IntPtr(b.ToInt64() + o))); }
        private static double ReadDouble(IntPtr b, long o) { byte[] x = new byte[8]; Marshal.Copy(new IntPtr(b.ToInt64() + o), x, 0, 8); return BitConverter.ToDouble(x, 0); }
        private static string ReadAnsi(IntPtr b, long o, int n) { byte[] x = new byte[n]; Marshal.Copy(new IntPtr(b.ToInt64() + o), x, 0, n); int e = Array.IndexOf<byte>(x, 0); if (e < 0) e = n; return Encoding.Default.GetString(x, 0, e).Trim(); }
        private sealed class SensorIdentity { public uint Id; public uint Instance; public string Name; }
    }
}
