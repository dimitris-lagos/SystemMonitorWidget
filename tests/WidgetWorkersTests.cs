using System;
using System.Collections;
using System.Reflection;
using System.Threading;

internal static class WidgetWorkersTests
{
    private static object Invoke(object target, string name, params object[] values)
    {
        try { return target.GetType().GetMethod(name).Invoke(target, values); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected widget EXE path.");
        Assembly assembly = Assembly.LoadFrom(args[0]);
        Type clientType = assembly.GetType("VegaDesktopWidget.FanControlClient", true);
        Type fanType = assembly.GetType("VegaDesktopWidget.FanCurveWorker", true);
        Type sensorType = assembly.GetType("VegaDesktopWidget.SensorPollingWorker", true);
        Type processType = assembly.GetType("VegaDesktopWidget.ProcessPollingWorker", true);
        Type profileType = assembly.GetType("VegaDesktopWidget.FanProfile", true);
        object client = Activator.CreateInstance(clientType, true);
        object fan = Activator.CreateInstance(fanType, new object[] { client });
        object sensor = Activator.CreateInstance(sensorType, new object[] { 500, null });
        object process = Activator.CreateInstance(processType, new object[] { 500, 0 });
        try
        {
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(profileType);
            IList noProfiles = (IList)Activator.CreateInstance(listType);
            Invoke(fan, "Configure", false, noProfiles, 500);
            Thread fanThread = (Thread)fanType.GetField("thread", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fan);
            Thread sensorThread = (Thread)sensorType.GetField("thread", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sensor);
            Thread processThread = (Thread)processType.GetField("thread", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(process);
            if (!fanThread.IsBackground || !sensorThread.IsBackground || !processThread.IsBackground) throw new Exception("Workers must be background threads.");
            if (fanThread.ManagedThreadId == sensorThread.ManagedThreadId || fanThread.ManagedThreadId == processThread.ManagedThreadId ||
                sensorThread.ManagedThreadId == processThread.ManagedThreadId || fanThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId ||
                sensorThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId || processThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
                throw new Exception("Workers are not independent.");
            object snapshot = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (snapshot == null && DateTime.UtcNow < deadline)
            {
                snapshot = Invoke(sensor, "TakeLatest", 0L);
                if (snapshot == null) Thread.Sleep(100);
            }
            if (snapshot == null) throw new Exception("No sensor snapshot was published.");
            long sequence = (long)snapshot.GetType().GetField("Sequence").GetValue(snapshot);
            if (sequence < 1 || Invoke(sensor, "TakeLatest", sequence) != null) throw new Exception("Snapshot sequence contract failed.");
            Type readingType = assembly.GetType("VegaDesktopWidget.SensorReading", true);
            Type readingListType = typeof(System.Collections.Generic.List<>).MakeGenericType(readingType);
            IList sample = (IList)Activator.CreateInstance(readingListType);
            sample.Add(Activator.CreateInstance(readingType, true));
            MethodInfo safeReadings = fanType.GetMethod("SafeReadings", BindingFlags.Static | BindingFlags.NonPublic);
            DateTime now = DateTime.UtcNow;
            IList fresh = (IList)safeReadings.Invoke(null, new object[] { sample, now, 5000, now });
            IList stale = (IList)safeReadings.Invoke(null, new object[] { sample, now.AddSeconds(-6), 5000, now });
            if (fresh.Count != 1 || stale.Count != 0) throw new Exception("Stale-temperature fail-safe contract failed.");
            MethodInfo safeForState = fanType.GetMethod("SafeReadingsForState", BindingFlags.Static | BindingFlags.NonPublic);
            IList startupGap = (IList)safeForState.Invoke(null, new object[] { sample, now.AddSeconds(-6), 5000, now, false, now.AddSeconds(90) });
            IList liveGap = (IList)safeForState.Invoke(null, new object[] { sample, now.AddSeconds(-6), 5000, now, true, now.AddSeconds(90) });
            if (startupGap.Count != 1 || liveGap.Count != 0) throw new Exception("Startup grace must retain readings without weakening the live fail-safe.");

            object startupProfile = Activator.CreateInstance(profileType, true);
            profileType.GetField("Enabled").SetValue(startupProfile, true);
            profileType.GetField("ControlId").SetValue(startupProfile, "/lpc/fan/0");
            profileType.GetField("TemperatureSensorKey").SetValue(startupProfile, "00000001:00000000:00000002");
            IList startupProfiles = (IList)Activator.CreateInstance(listType); startupProfiles.Add(startupProfile);
            IList startupReadings = (IList)Activator.CreateInstance(readingListType);
            MethodInfo temperaturesReady = clientType.GetMethod("TemperaturesReady", BindingFlags.Static | BindingFlags.NonPublic);
            if ((bool)temperaturesReady.Invoke(null, new object[] { startupProfiles, startupReadings })) throw new Exception("Fan control must wait before the first valid temperature sample.");
            Invoke(client, "Update", true, startupProfiles, startupReadings);
            string startupStatus = (string)clientType.GetProperty("Status").GetValue(client, null);
            bool startupConnected = (bool)clientType.GetProperty("IsConnected").GetValue(client, null);
            if (startupConnected || !startupStatus.StartsWith("Fan control waiting", StringComparison.Ordinal)) throw new Exception("Startup fan control changed hardware before temperatures were ready.");

            object startupReading = Activator.CreateInstance(readingType, true);
            readingType.GetField("SensorId").SetValue(startupReading, (uint)1);
            readingType.GetField("SensorInstance").SetValue(startupReading, (uint)0);
            readingType.GetField("ReadingId").SetValue(startupReading, (uint)2);
            readingType.GetField("Value").SetValue(startupReading, 42.0);
            readingType.GetField("Label").SetValue(startupReading, "CPU");
            readingType.GetField("OriginalLabel").SetValue(startupReading, "CPU");
            readingType.GetField("SensorName").SetValue(startupReading, "CPU");
            startupReadings.Add(startupReading);
            if (!(bool)temperaturesReady.Invoke(null, new object[] { startupProfiles, startupReadings })) throw new Exception("A valid first temperature sample must release startup fan control.");
            readingType.GetField("Value").SetValue(startupReading, Double.NaN);
            if ((bool)temperaturesReady.Invoke(null, new object[] { startupProfiles, startupReadings })) throw new Exception("Invalid first temperatures must not release startup fan control.");
            readingType.GetField("Value").SetValue(startupReading, 42.0);

            Invoke(fan, "Configure", false, startupProfiles, 1000);
            FieldInfo stableField = fanType.GetField("startupTemperaturesStable", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo readySinceField = fanType.GetField("startupReadySinceUtc", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo readingsUtcField = fanType.GetField("readingsUtc", BindingFlags.NonPublic | BindingFlags.Instance);
            Invoke(fan, "PublishReadings", startupReadings);
            if ((bool)stableField.GetValue(fan) || (DateTime)readingsUtcField.GetValue(fan) == DateTime.MinValue) throw new Exception("The first valid startup temperatures were not published immediately.");
            DateTime lastValidUtc = (DateTime)readingsUtcField.GetValue(fan);
            IList emptyReadings = (IList)Activator.CreateInstance(readingListType);
            Invoke(fan, "PublishReadings", emptyReadings);
            if ((DateTime)readingsUtcField.GetValue(fan) != lastValidUtc) throw new Exception("A transient empty sample replaced the last valid fan temperature snapshot.");
            readySinceField.SetValue(fan, DateTime.UtcNow.AddSeconds(-11));
            Invoke(fan, "PublishReadings", startupReadings);
            if (!(bool)stableField.GetValue(fan) || (DateTime)readingsUtcField.GetValue(fan) == DateTime.MinValue) throw new Exception("Stable temperatures did not release fan control.");
            Console.WriteLine("IndependentWorkers=PASS SensorSnapshot=PASS StaleTemperatureFailSafe=PASS StartupFanHold=PASS StartupGrace=PASS StartupTemperatureStability=PASS TransientSensorGap=PASS");
        }
        finally
        {
            Invoke(sensor, "Stop"); Invoke(process, "Stop"); Invoke(fan, "Stop");
        }
    }
}
