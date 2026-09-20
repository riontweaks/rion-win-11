using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.Adlx
{
    /// <summary>
    /// Abstraction over AMD's ADLX SDK for supported post-install monitoring / graphics /
    /// display / tuning / stress-test capabilities. Until a native ADLX bridge exists the
    /// only implementation is <see cref="NullAdlxService"/>, which reports nothing available.
    /// </summary>
    public interface IAdlxService
    {
        bool IsInitialized { get; }

        /// <summary>Detects which capabilities the current driver + GPU actually expose.</summary>
        AdlxCapabilitySet DetectCapabilities();

        /// <summary>One-line explanation shown wherever a capability is missing.</summary>
        string UnavailableReason { get; }
    }

    public sealed class NullAdlxService : IAdlxService
    {
        public bool IsInitialized => false;

        public AdlxCapabilitySet DetectCapabilities() => new AdlxCapabilitySet();

        public string UnavailableReason =>
            "ADLX controls are not available yet — the native ADLX bridge is not built into this version. " +
            "Use AMD Software: Adrenalin Edition for graphics, display and tuning settings for now.";
    }
}
