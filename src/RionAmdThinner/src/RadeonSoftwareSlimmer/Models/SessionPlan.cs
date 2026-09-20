using System;
using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Models
{
    public enum SessionStatus
    {
        Draft,
        Analyzed,
        Prepared,
        Installing,
        AwaitingReboot,
        PostInstall,
        Complete,
        Failed,
    }

    public sealed class SessionPlan
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12);
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public HardwareInfo HardwareSnapshot { get; set; }
        public DriverInstallerInfo InstallerInfo { get; set; }

        public List<string> KeptComponentIds { get; set; } = new List<string>();
        public List<string> ExcludedComponentIds { get; set; } = new List<string>();
        public List<string> PackageChanges { get; set; } = new List<string>();
        public List<string> PendingPostInstallChanges { get; set; } = new List<string>();

        public string OriginalInstallerPath { get; set; }
        public string WorkspacePath { get; set; }
        public string BackupManifestPath { get; set; }
        public List<string> UserApprovals { get; set; } = new List<string>();

        public SessionStatus Status { get; set; } = SessionStatus.Draft;
    }

    public sealed class AdlxCapabilitySet
    {
        public bool SupportsMonitoring { get; set; }
        public bool SupportsGraphics { get; set; }
        public bool SupportsDisplay { get; set; }
        public bool SupportsTuning { get; set; }
        public bool SupportsStressTest { get; set; }
        public List<string> SupportedFeatures { get; set; } = new List<string>();
        public Dictionary<string, string> HardwareReportedRanges { get; set; } = new Dictionary<string, string>();

        public bool AnythingAvailable =>
            SupportsMonitoring || SupportsGraphics || SupportsDisplay || SupportsTuning || SupportsStressTest;
    }
}
