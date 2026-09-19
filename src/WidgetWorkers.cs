using System;
using System.Collections.Generic;
using System.Threading;

namespace VegaDesktopWidget
{
    // Owns all potentially slow sensor and process reads. The UI only takes completed snapshots.
    internal sealed class SensorSnapshot
    {
        public long Sequence;
        public List<SensorReading> Readings;
        public string Status;
        public bool RamAvailable;
        public double RamUsed, RamTotal;
    }

    internal sealed class SensorPollingWorker
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread thread;
        private readonly Action<List<SensorReading>> onReadings;
        private SensorSnapshot latest;
        private bool stopping;
        private int interval;
        private long sequence;

        public SensorPollingWorker(int refreshMilliseconds, Action<List<SensorReading>> callback)
        {
            interval = refreshMilliseconds; onReadings = callback;
            thread = new Thread(Run); thread.IsBackground = true; thread.Name = "Widget sensor polling"; thread.Start();
        }

        public void Configure(int refreshMilliseconds)
        {
            lock (gate) interval = refreshMilliseconds;
            wake.Set();
        }

        public void RefreshNow() { wake.Set(); }

        public SensorSnapshot TakeLatest(long afterSequence)
        {
            lock (gate) return latest != null && latest.Sequence > afterSequence ? latest : null;
        }

        public void Stop()
        {
            lock (gate) stopping = true;
            wake.Set();
            // A temporarily blocked HWiNFO mutex must not freeze window shutdown.
            thread.Join(2000);
        }

        private void Run()
        {
            HWiNFOReader reader = new HWiNFOReader();
            while (true)
            {
                int delay;
                lock (gate) { if (stopping) return; delay = interval; }
                SensorSnapshot snapshot = new SensorSnapshot();
                try { snapshot.Readings = reader.Read(out snapshot.Status); }
                catch (Exception ex) { snapshot.Readings = new List<SensorReading>(); snapshot.Status = "Sensor read failed: " + ex.Message; }
                try { snapshot.RamAvailable = PhysicalMemory.Read(out snapshot.RamUsed, out snapshot.RamTotal); }
                catch { snapshot.RamAvailable = false; }
                lock (gate)
                {
                    if (stopping) return;
                    snapshot.Sequence = ++sequence; latest = snapshot;
                }
                if (onReadings != null) onReadings(snapshot.Readings);
                wake.WaitOne(Math.Max(100, delay));
            }
        }
    }

    internal sealed class ProcessPollingWorker
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread thread;
        private bool stopping;
        private int mode, interval;
        private List<ProcessUsage> latest = new List<ProcessUsage>();
        private long sequence;

        public ProcessPollingWorker(int refreshMilliseconds, int initialMode)
        {
            interval = refreshMilliseconds; mode = initialMode;
            thread = new Thread(Run); thread.IsBackground = true; thread.Name = "Widget process sampling"; thread.Start();
        }

        public void Configure(int refreshMilliseconds, int newMode)
        {
            lock (gate) { interval = refreshMilliseconds; mode = newMode; latest = new List<ProcessUsage>(); sequence++; }
            wake.Set();
        }

        public List<ProcessUsage> TakeLatest(ref long observedSequence)
        {
            lock (gate)
            {
                if (sequence <= observedSequence) return null;
                observedSequence = sequence; return latest;
            }
        }

        public void Stop()
        {
            lock (gate) stopping = true;
            wake.Set(); thread.Join(2000);
        }

        private void Run()
        {
            ProcessUsageSampler sampler = new ProcessUsageSampler();
            while (true)
            {
                int delay, selected;
                lock (gate) { if (stopping) return; delay = interval; selected = mode; }
                List<ProcessUsage> sampled;
                try { sampled = selected == 0 ? new List<ProcessUsage>() : sampler.SampleTop(3, selected == 1); }
                catch { sampled = new List<ProcessUsage>(); }
                lock (gate)
                {
                    if (stopping) return;
                    if (selected == mode) { latest = sampled; sequence++; }
                }
                wake.WaitOne(Math.Max(100, delay));
            }
        }
    }

    // Dedicated control loop. Its latest sensor sample can be stale, but the loop cannot stop
    // because painting, menus or process enumeration are slow.
    internal sealed class FanCurveWorker
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread thread;
        private readonly FanControlClient client;
        private bool stopping, enabled, resetRetry;
        private List<FanProfile> profiles = new List<FanProfile>();
        private List<SensorReading> readings = new List<SensorReading>();
        private DateTime readingsUtc = DateTime.MinValue;
        private int staleMilliseconds = 5000;

        internal static List<SensorReading> SafeReadings(List<SensorReading> current, DateTime sampledUtc, int maximumAgeMs, DateTime nowUtc)
        {
            return nowUtc - sampledUtc > TimeSpan.FromMilliseconds(maximumAgeMs) ? new List<SensorReading>() : current;
        }

        public FanCurveWorker(FanControlClient fanClient)
        {
            client = fanClient;
            thread = new Thread(Run); thread.IsBackground = true; thread.Name = "Widget fan curve"; thread.Start();
        }

        public void Configure(bool isEnabled, List<FanProfile> configured, int refreshMilliseconds)
        {
            List<FanProfile> copy = new List<FanProfile>();
            if (configured != null) foreach (FanProfile profile in configured) copy.Add(profile.Clone());
            lock (gate)
            {
                enabled = isEnabled; profiles = copy; resetRetry = true;
                staleMilliseconds = Math.Max(5000, refreshMilliseconds * 3);
            }
            wake.Set();
        }

        public void PublishReadings(List<SensorReading> fresh)
        {
            lock (gate) { readings = fresh ?? new List<SensorReading>(); readingsUtc = DateTime.UtcNow; }
            wake.Set();
        }

        public void Stop()
        {
            lock (gate) stopping = true;
            wake.Set();
            // The elevated helper restores BIOS defaults when its pipe disconnects.
            thread.Join(2000);
        }

        private void Run()
        {
            try
            {
                while (true)
                {
                    bool runEnabled, prepare;
                    List<FanProfile> currentProfiles;
                    List<SensorReading> currentReadings;
                    lock (gate)
                    {
                        if (stopping) return;
                        runEnabled = enabled; currentProfiles = profiles;
                        prepare = resetRetry; resetRetry = false;
                        currentReadings = SafeReadings(readings, readingsUtc, staleMilliseconds, DateTime.UtcNow);
                    }
                    try
                    {
                        if (prepare) client.PrepareConfigurationApply();
                        client.Update(runEnabled, currentProfiles, currentReadings);
                    }
                    catch { /* Client records recoverable errors; the loop must keep running. */ }
                    wake.WaitOne(1000);
                }
            }
            finally { client.Dispose(); }
        }
    }
}
