using System;
using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.Services
{
    public static class DriverValidation
    {
        public static bool IsReady(HardwareInfo info, string vendorId) => info != null
            && string.Equals(info.VendorId, vendorId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(info.DriverVersion)
            && info.DeviceErrorCode == 0
            && !string.IsNullOrWhiteSpace(info.DriverProvider)
            && (vendorId == "10DE" ? info.DriverProvider.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0
                : info.DriverProvider.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0
                  || info.DriverProvider.IndexOf("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
