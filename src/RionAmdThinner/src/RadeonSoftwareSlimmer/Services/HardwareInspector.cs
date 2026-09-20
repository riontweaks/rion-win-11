using System;
using System.Linq;
using System.Management;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Detects the GPU, PCI IDs, installed driver, Windows build/architecture, and
    /// laptop / hybrid-graphics / OEM-driver context via WMI + the registry.
    /// </summary>
    public sealed class HardwareInspector
    {
        public HardwareInfo Inspect(string preferredVendorId = "1002")
        {
            var info = new HardwareInfo();
            ReadOperatingSystem(info);
            ReadChassis(info);
            ReadVideoControllers(info, preferredVendorId);
            info.AmdSoftwareDetected = AmdInventory.IsInstalled(new WindowsRegistry());
            info.AmdSoftwareVersion = AmdInventory.SoftwareVersion(new WindowsRegistry());
            info.IsAdlxAvailable = false;   // filled in by the ADLX service once a bridge exists
            return info;
        }

        private static void ReadOperatingSystem(HardwareInfo info)
        {
            info.Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    string product = k?.GetValue("ProductName") as string;
                    string display = k?.GetValue("DisplayVersion") as string;
                    string build = k?.GetValue("CurrentBuild") as string;
                    int.TryParse(build, out int b);
                    if (b >= 22000 && product != null && product.Contains("Windows 10"))
                        product = product.Replace("Windows 10", "Windows 11");
                    info.WindowsVersion = ($"{product} {display}").Trim();
                    info.WindowsBuild = build;
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "OS detection failed");
                info.WindowsVersion = Environment.OSVersion.VersionString;
            }
        }

        private static void ReadChassis(HardwareInfo info)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                {
                    searcher.Options.Timeout = TimeSpan.FromSeconds(5);
                    using var results = searcher.Get();
                    foreach (ManagementObject mo in results)
                    using (mo)
                    {
                        if (mo["ChassisTypes"] is ushort[] types)
                        {
                            // 8=portable, 9=laptop, 10=notebook, 11=hand held, 14=sub-notebook, 30/31/32=tablet/convertible/detachable
                            int[] mobile = { 8, 9, 10, 11, 14, 30, 31, 32 };
                            info.IsLaptop |= types.Any(t => mobile.Contains(t));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Chassis detection failed");
            }
        }

        private static void ReadVideoControllers(HardwareInfo info, string preferredVendorId)
        {
            try
            {
                int adapterCount = 0;
                bool sawOtherVendor = false;

                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, PNPDeviceID, DriverVersion, DriverDate, AdapterCompatibility, ConfigManagerErrorCode FROM Win32_VideoController"))
                {
                    searcher.Options.Timeout = TimeSpan.FromSeconds(5);
                    using var results = searcher.Get();
                    foreach (ManagementObject mo in results)
                    using (mo)
                    {
                        adapterCount++;
                        string name = mo["Name"] as string ?? "";
                        string pnp = mo["PNPDeviceID"] as string ?? "";
                        string vendor = mo["AdapterCompatibility"] as string ?? "";

                        bool isAmd = name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0
                                     || name.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0
                                     || vendor.IndexOf("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) >= 0
                                     || pnp.IndexOf("VEN_1002", StringComparison.OrdinalIgnoreCase) >= 0;

                        bool isPreferred = preferredVendorId == "1002" ? isAmd
                            : pnp.IndexOf("VEN_" + preferredVendorId, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!isPreferred)
                        {
                            if (pnp.IndexOf("VEN_8086", StringComparison.OrdinalIgnoreCase) >= 0
                                || pnp.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) >= 0 || isAmd)
                                sawOtherVendor = true;
                            continue;
                        }

                        info.GpuName = name;
                        info.PnpDeviceId = pnp;
                        info.DriverProvider = vendor;
                        info.DriverVersion = mo["DriverVersion"] as string;
                        info.DeviceErrorCode = mo["ConfigManagerErrorCode"] is uint errorCode ? errorCode : null;
                        info.DriverDate = FormatWmiDate(mo["DriverDate"] as string);
                        ParsePciIds(pnp, info);
                    }
                }

                info.IsHybridGraphics = RequiresMultiAdapterReview(adapterCount, info.IsLaptop, sawOtherVendor, !string.IsNullOrEmpty(info.PnpDeviceId));
                // An OEM (INF provided by the laptop maker) is likely on a hybrid laptop, or when
                // the driver provider isn't AMD.
                info.IsOemDriverLikely = info.IsLaptop &&
                    (!string.IsNullOrEmpty(info.DriverProvider) &&
                     info.DriverProvider.IndexOf("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) < 0);
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "GPU detection failed");
            }
        }

        private static void ParsePciIds(string pnp, HardwareInfo info)
        {
            if (string.IsNullOrEmpty(pnp)) return;
            info.VendorId = Between(pnp, "VEN_", "&");
            info.DeviceId = Between(pnp, "DEV_", "&");
            info.SubsystemId = Between(pnp, "SUBSYS_", "&");
        }

        // Multiple adapters on a laptop require OEM review even when both are AMD.
        // Cross-vendor desktops are also reviewed; this is not proof of the display wiring.
        public static bool RequiresMultiAdapterReview(int count, bool laptop, bool otherVendor, bool selectedGpu) =>
            selectedGpu && count > 1 && (laptop || otherVendor);

        private static string Between(string s, string start, string end)
        {
            int i = s.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i += start.Length;
            int j = s.IndexOf(end, i, StringComparison.OrdinalIgnoreCase);
            return j < 0 ? s.Substring(i) : s.Substring(i, j - i);
        }

        private static string FormatWmiDate(string wmi)
        {
            if (string.IsNullOrEmpty(wmi) || wmi.Length < 8) return null;
            try
            {
                return $"{wmi.Substring(0, 4)}-{wmi.Substring(4, 2)}-{wmi.Substring(6, 2)}";
            }
            catch { return null; }
        }
    }
}
