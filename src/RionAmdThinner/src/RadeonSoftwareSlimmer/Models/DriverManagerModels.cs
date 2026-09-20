using System;
using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Models
{
    public enum WizardStep
    {
        Welcome = 0,
        SelectDriver = 1,
        Analyze = 2,
        Customize = 3,
        Review = 4,
        Configure = 5,   // choose post-install services + GPU tweaks, before the install runs
        Install = 6,     // launch + watch the install (progress shown in the pop-out monitor window)
        Finish = 7,
    }

    /// <summary>How a discovered component / service / task is classified by the rule engine.</summary>
    public enum Classification
    {
        Required,        // locked on, part of the core display driver
        KeepByDefault,   // on unless the user deliberately turns it off
        Optional,        // reasonable to exclude, feature loss is understood
        CandidateToDisable, // often unwanted, but never auto-disabled just for being present
        Unknown,         // schema not recognised — always kept
    }

    public enum ComponentAction
    {
        Keep,
        Exclude,
        ReviewAfterInstall,
        DoNotInstallIfPackageExcluded,
    }

    /// <summary>Evidence policy grade. Grade C/D are never described as "verified".</summary>
    public enum EvidenceGrade
    {
        A_Official,          // AMD/Microsoft docs, signed package metadata
        B_ReproduciblyTested,// automated tests, documented across named versions
        C_CommunityInformed, // RSS wiki, public source behaviour, credible reports
        D_Unverified,        // no reliable support — no recommendation, keep by default
    }

    public enum SignatureStatus
    {
        NotChecked,
        Valid,
        ValidButNotAmd,
        Invalid,
        Unsigned,
        Error,
    }

    public enum InstallVerification
    {
        NotRun,
        Passed,
        PartiallyPassed,
        Failed,
    }

    public sealed class ServiceRecord
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ExecutablePath { get; set; }
        public string StartType { get; set; }
        public string CurrentState { get; set; }
        public List<string> AssociatedComponents { get; set; } = new List<string>();
        public string RecommendationId { get; set; }
        public string BeforeState { get; set; }
        public string AfterState { get; set; }
    }

    public sealed class ScheduledTaskRecord
    {
        public string TaskPath { get; set; }
        public string Triggers { get; set; }
        public string Actions { get; set; }
        public bool Enabled { get; set; }
        public string LastRunTime { get; set; }
        public string LastTaskResult { get; set; }
        public List<string> AssociatedComponents { get; set; } = new List<string>();
        public string RecommendationId { get; set; }
        public string BeforeState { get; set; }
        public string AfterState { get; set; }
    }

    public sealed class StartupRecord
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public string Command { get; set; }
        public bool Enabled { get; set; }
        public List<string> AssociatedComponents { get; set; } = new List<string>();
        public string RecommendationId { get; set; }
    }

    public sealed class AuditLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string UserAction { get; set; }
        public string Operation { get; set; }
        public string Target { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Result { get; set; }
        public string ErrorDetails { get; set; }
        public bool Reversible { get; set; }
        public string SessionId { get; set; }
    }
}
