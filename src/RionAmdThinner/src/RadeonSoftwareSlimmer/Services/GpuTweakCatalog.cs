using System.Collections.Generic;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// The curated set of AMD GPU registry tweaks the app can apply after a verified install.
    /// Deliberately small: only latency / stability / driver-retention changes that are
    /// documented and reversible. No experimental clock-gating / power-play edits.
    /// </summary>
    public static class GpuTweakCatalog
    {
        public static IReadOnlyList<GpuTweak> All { get; } = new List<GpuTweak>
        {
            new GpuTweak
            {
                Id = "tdr-delay",
                Title = "Longer GPU recovery timeout (TDR)",
                Summary = "Raises the display-driver response timeout from the Windows default of 2 seconds to 10 seconds, "
                          + "so a brief driver stall recovers instead of causing a black screen or crash.",
                TradeOff = "A genuinely hung GPU takes a few seconds longer to reset.",
                EvidenceGrade = EvidenceGrade.C_CommunityInformed,
                SourceNote = "Microsoft TDR registry keys (Timeout Detection and Recovery); widely used for AMD stability.",
                RecommendedDefault = false,   // opt-in only - nothing GPU-registry is auto-checked
                Location = GpuTweakLocation.GraphicsDrivers,
                ValueName = "TdrDelay",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 10,
                NeutralValue = null, // absent by default
            },
            new GpuTweak
            {
                Id = "tdr-ddi-delay",
                Title = "Longer GPU DDI timeout (TDR)",
                Summary = "Companion to the TDR delay: extends the device-driver-interface timeout to 10 seconds so heavy "
                          + "shader compilation or a momentary stall does not trip a driver reset.",
                TradeOff = "Same as the TDR delay - slightly slower recovery from a real hang.",
                EvidenceGrade = EvidenceGrade.C_CommunityInformed,
                SourceNote = "Microsoft TDR registry keys.",
                RecommendedDefault = false,   // opt-in only - nothing GPU-registry is auto-checked
                Location = GpuTweakLocation.GraphicsDrivers,
                ValueName = "TdrDdiDelay",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 10,
                NeutralValue = null,
            },
            new GpuTweak
            {
                Id = "disable-ulps",
                Title = "Disable ULPS (Ultra Low Power State)",
                Summary = "Stops the GPU from dropping into its deepest idle power state. On many Radeon setups this "
                          + "removes idle/multi-monitor flicker and wake-from-idle stutter.",
                TradeOff = "A few watts more power at idle.",
                EvidenceGrade = EvidenceGrade.C_CommunityInformed,
                SourceNote = "Long-standing community fix for Radeon idle flicker; EnableUlps on the adapter class key.",
                RecommendedDefault = false,   // opt-in only - nothing GPU-registry is auto-checked
                Location = GpuTweakLocation.AmdAdapterClassKey,
                ValueName = "EnableUlps",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 0,
                NeutralValue = 1, // AMD default is enabled
            },
            new GpuTweak
            {
                Id = "disable-mpo",
                Title = "Disable Multiplane Overlay (MPO)",
                Summary = "Turns off the Desktop Window Manager's Multiplane Overlay path via Dwm\\OverlayTestMode = 5 - "
                          + "the method the AMD community uses to fix MPO flickering, black flashes and stutter on Radeon + Windows 11.",
                TradeOff = "OverlayTestMode is an undocumented DWM value. It works for most people, but on some displays it can "
                           + "cause faint horizontal banding - if you see any screen artifacts after applying, revert this one first.",
                EvidenceGrade = EvidenceGrade.C_CommunityInformed,
                SourceNote = "Widely-cited MPO fix (AMD/Nvidia community, TechPowerUp, Guru3D); Dwm\\OverlayTestMode = 5.",
                RecommendedDefault = false,
                Location = GpuTweakLocation.Dwm,
                ValueName = "OverlayTestMode",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 5,
                NeutralValue = null,
            },
            new GpuTweak
            {
                Id = "block-wu-driver",
                Title = "Stop Windows Update replacing this driver",
                Summary = "Prevents Windows Update from silently swapping the driver you just installed for an older "
                          + "WHQL build during quality updates.",
                TradeOff = "You update the GPU driver yourself from now on (which is the point of this app).",
                EvidenceGrade = EvidenceGrade.A_Official,
                SourceNote = "Documented Windows Update policy: ExcludeWUDriversInQualityUpdate.",
                RecommendedDefault = false,
                Location = GpuTweakLocation.WindowsUpdatePolicy,
                ValueName = "ExcludeWUDriversInQualityUpdate",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 1,
                NeutralValue = 0,
            },
            new GpuTweak
            {
                Id = "enable-hags",
                Title = "Enable Hardware-Accelerated GPU Scheduling (HAGS)",
                Summary = "Turns on the Windows feature that lets the GPU manage its own scheduling and video memory. "
                          + "Can reduce latency on some systems; can add micro-stutter on others.",
                TradeOff = "Effects vary by system, game and driver - test it. It is the same switch as Windows Settings > "
                           + "System > Display > Graphics > 'Hardware-accelerated GPU scheduling', so you can also toggle it there.",
                EvidenceGrade = EvidenceGrade.A_Official,
                SourceNote = "Windows setting: GraphicsDrivers\\HwSchMode (2 = on, 1 = off). Reboot required.",
                RecommendedDefault = false,
                Location = GpuTweakLocation.GraphicsDrivers,
                ValueName = "HwSchMode",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 2,
                NeutralValue = 1,
            },
            new GpuTweak
            {
                Id = "disable-power-throttling",
                Title = "Disable Windows Power Throttling",
                Summary = "Stops Windows from throttling background-process CPU to save power. A common desktop gaming / "
                          + "low-latency tweak.",
                TradeOff = "Higher idle power draw and heat. Leave this off on laptops / handhelds - it hurts battery life.",
                EvidenceGrade = EvidenceGrade.A_Official,
                SourceNote = "Documented Windows power management: Power\\PowerThrottling\\PowerThrottlingOff.",
                RecommendedDefault = false,
                Location = GpuTweakLocation.PowerThrottling,
                ValueName = "PowerThrottlingOff",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 1,
                NeutralValue = 0,
            },
            new GpuTweak
            {
                Id = "amd-telemetry-off",
                Title = "Disable AMD driver telemetry",
                Summary = "Sets AMD Software's Telemetry flag to off so Radeon Software stops sending usage data.",
                TradeOff = "None visible. Pair it with 'Disable AMD usage-data collection' below.",
                EvidenceGrade = EvidenceGrade.C_CommunityInformed,
                SourceNote = "HKLM\\SOFTWARE\\AMD\\CN Telemetry - observed AMD Software behaviour.",
                RecommendedDefault = false,
                Location = GpuTweakLocation.AmdConfig,
                ValueName = "Telemetry",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 0,
                NeutralValue = 1,
            },
            new GpuTweak
            {
                Id = "amd-collectgi-off",
                Title = "Disable AMD usage-data collection",
                Summary = "Sets AMD Software's CollectGIData flag to off (the companion to the telemetry flag).",
                TradeOff = "None visible.",
                EvidenceGrade = EvidenceGrade.C_CommunityInformed,
                SourceNote = "HKLM\\SOFTWARE\\AMD\\CN CollectGIData - observed AMD Software behaviour.",
                RecommendedDefault = false,
                Location = GpuTweakLocation.AmdConfig,
                ValueName = "CollectGIData",
                ValueKind = RegistryValueKind.DWord,
                AppliedValue = 0,
                NeutralValue = 1,
            },
        };
    }
}
