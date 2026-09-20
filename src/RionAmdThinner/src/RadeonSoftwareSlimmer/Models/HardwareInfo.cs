namespace RadeonSoftwareSlimmer.Models
{
    /// <summary>Everything the app knows about the machine it is running on.</summary>
    public sealed class HardwareInfo
    {
        public string GpuName { get; set; }
        public string VendorId { get; set; }        // e.g. "1002" (AMD)
        public string DeviceId { get; set; }
        public string SubsystemId { get; set; }
        public string PnpDeviceId { get; set; }
        public string DriverVersion { get; set; }
        public string DriverDate { get; set; }
        public string DriverProvider { get; set; }
        public uint? DeviceErrorCode { get; set; }

        public string WindowsVersion { get; set; }
        public string WindowsBuild { get; set; }
        public string Architecture { get; set; }

        public bool IsLaptop { get; set; }
        public bool IsHybridGraphics { get; set; }
        public bool IsOemDriverLikely { get; set; }
        public bool IsAdlxAvailable { get; set; }
        public bool AmdSoftwareDetected { get; set; }
        public string AmdSoftwareVersion { get; set; }

        public bool IsAmdGpu =>
            !string.IsNullOrEmpty(VendorId) &&
            VendorId.Equals("1002", System.StringComparison.OrdinalIgnoreCase);

        public string OemWarning =>
            IsLaptop || IsHybridGraphics || IsOemDriverLikely
                ? "Your device may rely on manufacturer-customized graphics drivers. " +
                  "Review your OEM support page before installing a generic AMD driver."
                : null;
    }
}
