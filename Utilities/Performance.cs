using System.Collections.Generic;
using System.Diagnostics;

namespace Receiver.Utilities
{
    class Performance
    {
        public static PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        public static PerformanceCounter ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use", null);
        public static PerformanceCounter diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
        public static List<PerformanceCounter> subCpus = new List<PerformanceCounter>();
        public static float[] getPerformaceCounter()
        {

            // will always start at 0
            _ = cpuCounter.NextValue();
            _ = ramCounter.NextValue();
            _ = diskCounter.NextValue();
            foreach (var subCpu in subCpus)
            {
                _ = subCpu.NextValue();
            }
            System.Threading.Thread.Sleep(500);
            // now matches task manager reading

            List<float> val = new List<float>();
            val.Add(cpuCounter.NextValue());
            val.Add(ramCounter.NextValue());
            val.Add(diskCounter.NextValue());
            foreach (var subCpu in subCpus)
            {
                val.Add(subCpu.NextValue());
            }
            return val.ToArray();

        }
    }
}
