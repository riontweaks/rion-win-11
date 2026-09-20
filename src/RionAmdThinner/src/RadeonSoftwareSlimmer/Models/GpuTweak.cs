using Microsoft.Win32;

namespace RadeonSoftwareSlimmer.Models
{
    /// <summary>Where a GPU tweak's value lives. The concrete key path is resolved at apply time.</summary>
    public enum GpuTweakLocation
    {
        /// <summary>HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers</summary>
        GraphicsDrivers,
        /// <summary>HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-...}\NNNN (the AMD adapter)</summary>
        AmdAdapterClassKey,
        /// <summary>HKLM\SOFTWARE\Microsoft\Windows\Dwm</summary>
        Dwm,
        /// <summary>HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate</summary>
        WindowsUpdatePolicy,
        /// <summary>HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling</summary>
        PowerThrottling,
        /// <summary>HKLM\SOFTWARE\AMD\CN</summary>
        AmdConfig,
    }

    /// <summary>
    /// One curated, reversible AMD GPU registry tweak. Every entry is documented, evidence-graded,
    /// and stores enough to undo itself (previous value is captured to the backup store before writing).
    /// These are applied only after a verified driver install and only with elevation.
    /// </summary>
    public sealed class GpuTweak
    {
        public string Id { get; set; }
        public string Title { get; set; }

        /// <summary>Plain-English: what it changes and why. One or two sentences.</summary>
        public string Summary { get; set; }

        /// <summary>What is lost / the trade-off if it is applied.</summary>
        public string TradeOff { get; set; }

        public EvidenceGrade EvidenceGrade { get; set; } = EvidenceGrade.C_CommunityInformed;
        public string SourceNote { get; set; }

        /// <summary>Ticked by default in the Install &amp; Verify list.</summary>
        public bool RecommendedDefault { get; set; }

        public GpuTweakLocation Location { get; set; }
        public string ValueName { get; set; }
        public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.DWord;

        /// <summary>Value written when the tweak is applied.</summary>
        public long AppliedValue { get; set; }

        /// <summary>
        /// Value written on revert when the tweak's key did not previously have this value.
        /// When null, revert deletes the value instead of writing anything.
        /// </summary>
        public long? NeutralValue { get; set; }

        public string EvidenceGradeLabel
        {
            get
            {
                switch (EvidenceGrade)
                {
                    case EvidenceGrade.A_Official: return "A - Official documentation";
                    case EvidenceGrade.B_ReproduciblyTested: return "B - Reproducibly tested";
                    case EvidenceGrade.C_CommunityInformed: return "C - Community-informed";
                    default: return "D - Not fully confirmed";
                }
            }
        }
    }
}
