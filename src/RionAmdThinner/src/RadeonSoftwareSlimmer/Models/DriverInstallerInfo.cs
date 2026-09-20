using System;

namespace RadeonSoftwareSlimmer.Models
{
    public sealed class DriverInstallerInfo
    {
        public string FilePath { get; set; }
        public string OriginalHashSha256 { get; set; }
        public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.NotChecked;
        public string Publisher { get; set; }
        public string DriverVersion { get; set; }
        public string PackageVersion { get; set; }
        public string DownloadSource { get; set; }
        public DateTime? DownloadedAt { get; set; }
        public long FileSizeBytes { get; set; }
        public bool IsOfficialAmdSource { get; set; }

        public string FileSizeDisplay =>
            FileSizeBytes <= 0 ? "—" :
            FileSizeBytes >= 1L << 30 ? $"{FileSizeBytes / (double)(1L << 30):0.0} GB" :
            $"{FileSizeBytes / (double)(1L << 20):0} MB";

        public bool LooksTrusted =>
            Services.HistoricalDriverInstaller.Trusted("AMD", this);
    }
}
