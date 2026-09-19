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
            Console.WriteLine("IndependentWorkers=PASS SensorSnapshot=PASS StaleTemperatureFailSafe=PASS");
        }
        finally
        {
            Invoke(sensor, "Stop"); Invoke(process, "Stop"); Invoke(fan, "Stop");
        }
    }
}
