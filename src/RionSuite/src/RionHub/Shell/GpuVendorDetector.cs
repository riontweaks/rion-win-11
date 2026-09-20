using System.Management;

namespace RionHub.Shell;

internal readonly record struct GpuVendors(bool Amd, bool Nvidia, bool Intel = false);

internal static class GpuVendorDetector
{
    // PCI IDs work even when Windows is using its basic display driver.
    internal static GpuVendors Classify(IEnumerable<string> deviceIds)
    {
        bool amd = false, nvidia = false, intel = false;
        foreach (string id in deviceIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            amd |= id.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase);
            nvidia |= id.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase);
            intel |= id.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase);
        }
        return new(amd, nvidia, intel);
    }

    internal static GpuVendors Detect()
    {
        using var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController");
        searcher.Options.Timeout = TimeSpan.FromSeconds(5);
        using var adapters = searcher.Get();
        var ids = new List<string>();
        foreach (ManagementObject adapter in adapters)
        {
            using (adapter) ids.Add(adapter["PNPDeviceID"]?.ToString() ?? "");
        }
        return Classify(ids);
    }
}
