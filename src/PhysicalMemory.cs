using System;
using System.Runtime.InteropServices;

namespace VegaDesktopWidget
{
    internal static class PhysicalMemory
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MemoryStatus
        {
            public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus status);

        public static bool Read(out double usedGb, out double totalGb)
        {
            MemoryStatus status = new MemoryStatus();
            if (!GlobalMemoryStatusEx(status)) { usedGb = 0; totalGb = 0; return false; }
            const double bytesPerGb = 1073741824.0;
            totalGb = status.TotalPhysical / bytesPerGb;
            usedGb = (status.TotalPhysical - status.AvailablePhysical) / bytesPerGb;
            return true;
        }
    }
}
