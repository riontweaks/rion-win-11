using System;
using System.Collections.Generic;
using System.IO;
using RadeonSoftwareSlimmer.Intefaces;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Central place for "what does AMD install" knowledge: install detection, version strings,
    /// and the curated service / scheduled-task / process name lists (carried over from
    /// RadeonSoftwareSlimmer and the project wiki) with human descriptions and risk notes.
    /// </summary>
    public static class AmdInventory
    {
        private const string CnRegistryKey = "SOFTWARE\\AMD\\CN";

        public static bool IsInstalled(IRegistry registry)
        {
            return !string.IsNullOrEmpty(GetInstallDirectory(registry));
        }

        public static string GetInstallDirectory(IRegistry registry)
        {
            try
            {
                using (IRegistryKey key = registry.LocalMachine.OpenSubKey(CnRegistryKey, false))
                {
                    string dir = key?.GetValue("InstallDir") as string;
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        return dir;
                }
            }
            catch { /* fall through to default */ }

            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AMD", "CNext", "CNext");
            return Directory.Exists(fallback) ? fallback : null;
        }

        public static string GetCnValue(IRegistry registry, string valueName)
        {
            try
            {
                using (IRegistryKey key = registry.LocalMachine.OpenSubKey(CnRegistryKey, false))
                {
                    return key?.GetValue(valueName)?.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        // Display-adapter class GUID.
        public const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
        private const string ClassRoot = "SYSTEM\\CurrentControlSet\\Control\\Class\\" + DisplayClassGuid;

        private static string _gpuClassKeyPath;

        /// <summary>
        /// Resolves the driver "class" subkey (…\Class\{4d36e968…}\0000, 0001, …) that belongs to the
        /// installed AMD/Radeon adapter — the one whose "DriverDesc" mentions AMD/Radeon/ATI.
        /// Falls back to \0000. Cached for the process.
        /// </summary>
        public static string GetGpuClassKeyPath(IRegistry registry)
        {
            if (_gpuClassKeyPath != null)
                return _gpuClassKeyPath;

            try
            {
                using (IRegistryKey root = registry.LocalMachine.OpenSubKey(ClassRoot, false))
                {
                    if (root != null)
                    {
                        foreach (string sub in root.GetSubKeyNames())
                        {
                            if (sub.Length != 4)
                                continue;   // 0000, 0001, … only

                            using (IRegistryKey child = root.OpenSubKey(sub, false))
                            {
                                string desc = child?.GetValue("DriverDesc") as string;
                                string provider = child?.GetValue("ProviderName") as string;
                                if (Mentions(desc) || Mentions(provider))
                                {
                                    _gpuClassKeyPath = ClassRoot + "\\" + sub;
                                    return _gpuClassKeyPath;
                                }
                            }
                        }
                    }
                }
            }
            catch { /* fall through */ }

            _gpuClassKeyPath = ClassRoot + "\\0000";
            return _gpuClassKeyPath;
        }

        private static bool Mentions(string value) =>
            !string.IsNullOrEmpty(value) &&
            (value.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("ATI", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Best-effort GPU model name from the resolved class key.</summary>
        public static string GpuName(IRegistry registry)
        {
            try
            {
                using (IRegistryKey key = registry.LocalMachine.OpenSubKey(GetGpuClassKeyPath(registry), false))
                    return key?.GetValue("DriverDesc") as string;
            }
            catch
            {
                return null;
            }
        }

        public static string DriverVersion(IRegistry registry) => GetCnValue(registry, "DriverVersion");
        public static string SoftwareVersion(IRegistry registry) => GetCnValue(registry, "CNVersion");
        public static bool IsAdrenalin(IRegistry registry) =>
            string.Equals(GetCnValue(registry, "Adrenalin"), "True", StringComparison.OrdinalIgnoreCase);

        /// <summary>System services / drivers installed by Radeon Software. Not every machine has all of them.</summary>
        public static readonly IReadOnlyList<AmdServiceInfo> Services = new List<AmdServiceInfo>
        {
            new AmdServiceInfo("amdfendr", "AMD Crash Defender driver", "Crash-dump logging for AMD troubleshooting. Safe to disable.", TweakRiskLevel.Safe, true),
            new AmdServiceInfo("amdfendrmgr", "AMD Crash Defender manager driver", "Manages the Crash Defender services. Safe to disable.", TweakRiskLevel.Safe, true),
            new AmdServiceInfo("amdlog", "AMD Crash Defender log driver", "Companion logging driver for Crash Defender. Safe to disable.", TweakRiskLevel.Safe, true),
            new AmdServiceInfo("AMDXE", "AMD Link Xinput emulation driver", "Emulates an Xbox 360 controller for AMD Link streaming. Safe to disable if you don't use AMD Link.", TweakRiskLevel.Safe, true),
            new AmdServiceInfo("amdacpbus", "AMD Audio CoProcessor bus driver", "Audio CoProcessor bus. Leave enabled if you use the GPU/APU audio path.", TweakRiskLevel.Caution, true),
            new AmdServiceInfo("AMDAcpBtAudioService", "AMD ACP Bluetooth audio filter", "Bluetooth audio profiles (HFP/A2DP) via the AMD audio coprocessor.", TweakRiskLevel.Caution, false),
            new AmdServiceInfo("AMDAfdAudioService", "AMD AFD audio function driver", "Wave/topology microphone support via the AMD audio coprocessor.", TweakRiskLevel.Caution, false),
            new AmdServiceInfo("AMDHDAudBusService", "AMD High Definition Audio bus", "HD Audio bus for the AMD audio coprocessor.", TweakRiskLevel.Caution, false),
            new AmdServiceInfo("amdi2stdmafd", "AMD I2S TDM filter driver", "I2S TDM controller support (embedded / handheld).", TweakRiskLevel.Safe, true),
            new AmdServiceInfo("AMDSoundWireAudioService", "AMD SoundWire filter driver", "Streams audio to Android devices. Safe to disable if unused.", TweakRiskLevel.Safe, false),
            new AmdServiceInfo("AtiHDAudioService", "AMD HDMI Audio driver", "HDMI / DisplayPort audio output from the GPU. Disable only if you never use display audio.", TweakRiskLevel.Caution, true),
            new AmdServiceInfo("AMDSAFD", "AMD Streaming Audio driver", "Backs AMD Link audio streaming and AMD Noise Suppression (microphone noise removal). Disable only if you don't use either.", TweakRiskLevel.Caution, true),
            new AmdServiceInfo("AMD Crash Defender Service", "AMD Crash Defender Service", "User-mode side of Crash Defender crash logging. Safe to disable.", TweakRiskLevel.Safe, false),
            new AmdServiceInfo("AMD External Events Utility", "AMD External Events Utility", "Monitors the system for FreeSync, Chill, FRTC and Anti-Lag activation. Disabling BREAKS those features.", TweakRiskLevel.Caution, false),
            new AmdServiceInfo("AMD Log Utility", "AMD Log Utility", "Companion logging service. Safe to disable.", TweakRiskLevel.Safe, false),
            new AmdServiceInfo("AUEPLauncher", "AMD User Experience Program launcher", "Collects usage data to 'improve products'. Safe to disable.", TweakRiskLevel.Safe, false),
            new AmdServiceInfo("AMDRadeonSettings", "AMD Radeon Settings", "Helper service for Radeon Settings. Safe to disable; the UI still opens.", TweakRiskLevel.Safe, false),
            new AmdServiceInfo("amducsi", "AMD UCM-UCSI device", "USB Type-C connector management on newer Radeon cards.", TweakRiskLevel.Caution, true),
            new AmdServiceInfo("SSGService", "Radeon Pro SSG", "NVMe storage expansion on Radeon Pro SSG cards. Safe to disable on consumer cards.", TweakRiskLevel.Safe, false),
        };

        /// <summary>Scheduled tasks Radeon Software registers. Matched by name or by Author = 'Advanced Micro Devices'.</summary>
        public static readonly IReadOnlyList<string> ScheduledTaskNames = new[]
        {
            "DVRAnalytics", "AMDDVRAnalytics", "StartAUEP", "StartCN", "StartCNBM", "StartDVR",
            "AMD COMPUTE", "AMDInstallLauncher", "AMDInstallUEP", "AMD UWP Launcher",
            "AMD Link Driver", "AMDRelauncher", "AMDRyzenMasterSDKTask", "AMD Updater", "ModifyLinkUpdate",
        };

        public const string TaskAuthor = "Advanced Micro Devices";

        /// <summary>Background processes that make up Radeon Software while it is running.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> HostProcesses = new[]
        {
            new KeyValuePair<string, string>("RadeonSoftware", "Radeon Software: host application"),
            new KeyValuePair<string, string>("AMDRSServ", "Radeon Settings: host service"),
            new KeyValuePair<string, string>("amdow", "Radeon Settings: desktop overlay"),
            new KeyValuePair<string, string>("AMDRSSrcExt", "Radeon Settings: source extension"),
        };
    }

    public enum TweakRiskLevel
    {
        Safe,
        Caution,
    }

    public sealed class AmdServiceInfo
    {
        public AmdServiceInfo(string name, string displayName, string description, TweakRiskLevel risk, bool isDriver)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
            Risk = risk;
            IsDriver = isDriver;
        }

        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public TweakRiskLevel Risk { get; }
        public bool IsDriver { get; }
    }
}
