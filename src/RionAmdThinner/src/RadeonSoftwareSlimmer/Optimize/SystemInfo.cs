using System;
using System.Management;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Optimize
{
    public static class SystemInfo
    {
        /// <summary>
        /// Total physical memory reported by Windows in KB, before RAM-tier normalization.
        /// Returns zero if WMI is unavailable; never invent a RAM capacity.
        /// </summary>
        public static long TotalPhysicalMemoryKb()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        ulong bytes = Convert.ToUInt64(mo["TotalPhysicalMemory"]);
                        if (bytes > 0)
                            return (long)(bytes / 1024UL);
                    }
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not read total physical memory");
            }
            return 0;
        }

        /// <summary>
        /// Supported RAM capacities and the service-host threshold each one maps to.
        /// Values supplied by the owner are hexadecimal DWORDs measured in KB.
        /// </summary>
        private static readonly (int CapacityGb, long ThresholdKb)[] Tiers =
        {
            (4, 0x400000),
            (6, 0x600000),
            (8, 0x800000),
            (12, 0xC00000),
            (16, 0x1000000),
            (24, 0x1800000),
            (32, 0x2000000),
            (64, 0x4000000),
        };

        /// <summary>Service-host threshold for the nearest supported RAM tier.</summary>
        public static long ServiceHostSplitThresholdKb() =>
            ServiceHostSplitThresholdKb(TotalPhysicalMemoryKb());

        /// <summary>
        /// Maps detected KB to the nearest listed RAM capacity and returns that capacity's
        /// table threshold. Exact ties select the higher tier. Unlisted large capacities use
        /// rounded GiB converted to KB rather than an unrelated smaller table entry.
        /// </summary>
        public static long ServiceHostSplitThresholdKb(long detectedKb)
        {
            if (detectedKb <= 0)
                return 0x380000;

            if (detectedKb > 64L * 1024 * 1024)
                return Math.Min(uint.MaxValue, (long)Math.Ceiling(detectedKb / (1024d * 1024)) * 1024 * 1024);

            var selected = Tiers[0];
            foreach (var tier in Tiers)
            {
                long candidateKb = tier.CapacityGb * 1024L * 1024;
                long selectedKb = selected.CapacityGb * 1024L * 1024;
                if (Math.Abs(detectedKb - candidateKb) <= Math.Abs(detectedKb - selectedKb))
                    selected = tier;
            }
            return selected.ThresholdKb;
        }

        /// <summary>Rounded GB, for display.</summary>
        public static int TotalPhysicalMemoryGb()
        {
            long kb = TotalPhysicalMemoryKb();
            return (int)Math.Round(kb / 1024.0 / 1024.0);
        }
    }
}
