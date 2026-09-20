using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Intefaces;
using RadeonSoftwareSlimmer.Services;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Optimize
{
    public sealed class TrimmableService
    {
        public string Name { get; set; }
        public string Note { get; set; }
        /// <summary>2 = Automatic, 3 = Manual, 4 = Disabled.</summary>
        public int TargetStart { get; set; } = 3;
    }

    /// <summary>
    /// Curated optional Windows services set to Manual to reduce automatic startup
    /// activity when applicable. Applied by writing the service's <c>Start</c> value under
    /// HKLM\SYSTEM\CurrentControlSet\Services, captured to the backup store so it is reversible.
    /// Protected services are excluded. Manual services can still start on demand; no fixed process reduction is promised.
    /// </summary>
    public static class ServiceTrimCatalog
    {
        private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services\";
        public static string StartLabel(int start)
        {
            switch (start)
            {
                case 2: return "Automatic";
                case 3: return "Manual";
                case 4: return "Disabled";
                default: return "Unknown";
            }
        }

        /// <summary>Current Start value (2/3/4) for one service, or null if the service key/value
        /// is absent on this edition.</summary>
        public static int? GetCurrentStart(IRegistry registry, string serviceName)
        {
            try
            {
                using (IRegistryKey key = registry.LocalMachine.OpenSubKey(ServicesKey + serviceName, false))
                {
                    object v = key?.GetValue("Start", null);
                    return v == null ? (int?)null : Convert.ToInt32(v, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not read Start for service " + serviceName);
                return null;
            }
        }

        /// <summary>
        /// Writes one service's Start value — the single-service unit the bulk <see cref="Apply"/>
        /// loop and the granular Services Manager UI both call. Skips (returns false) if the
        /// service is absent on this edition or already at <paramref name="newStart"/>; otherwise
        /// backs up the previous value and audit-logs the change.
        /// </summary>
        public static bool SetStart(IRegistry registry, string serviceName, int newStart, BackupSession backup, string source = "Services Manager")
        {
            if (newStart < 2 || newStart > 4 || string.IsNullOrWhiteSpace(serviceName)
                || serviceName.IndexOfAny(new[] { '\\', '/', '\0' }) >= 0) return false;
            if (!Elevation.IsElevated) return false;

            string path = ServicesKey + serviceName;
            try
            {
                using (IRegistryKey key = registry.LocalMachine.OpenSubKey(path, true))
                {
                    if (key == null) return false;

                    object cur = key.GetValue("Start", null);
                    if (cur == null) return false;

                    long current = Convert.ToInt64(cur, CultureInfo.InvariantCulture);
                    if (current < 2 || current > 4) return false; // never rewrite boot/system driver start types
                    if (current == newStart) return false;
                    // Bulk/manual trimming cannot weaken protected services. Dedicated repair
                    // and explicit feature toggles retain their own existing behavior.
                    bool trimming = source == "Auto-Optimize" || source == "Services Manager";
                    if (trimming && newStart > current && ServiceProtection.Reason(serviceName) != null) return false;
                    int serviceType = Convert.ToInt32(key.GetValue("Type", 0), CultureInfo.InvariantCulture);
                    if (trimming && (serviceType & 48) == 0) return false; // not a Win32 service
                    bool userTemplate = (serviceType & 64) != 0 && (serviceType & 128) == 0;
                    if (trimming && newStart > current && !userTemplate)
                    {
                        using (var controller = new System.ServiceProcess.ServiceController(serviceName))
                        {
                            var dependents = controller.DependentServices;
                            try { if (dependents.Length > 0) return false; }
                            finally { foreach (var dependent in dependents) dependent.Dispose(); }
                        }
                    }

                    RegistryChangeHistory.Write(backup, key, "HKLM\\" + path, "Start", true, newStart, RegistryValueKind.DWord);

                    AuditLog.Action(source, "ServiceStart", serviceName,
                        oldValue: current.ToString(CultureInfo.InvariantCulture),
                        newValue: newStart.ToString(CultureInfo.InvariantCulture),
                        result: "OK", reversible: true);
                    StaticViewModel.AddLogMessage($"Service {serviceName} → {StartLabel(newStart)}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not set Start for service " + serviceName);
                return false;
            }
        }

        /// <summary>
        /// Applies the trim set. Returns (applied service notes, already-at-target notes). Every
        /// change writes the prior <c>Start</c> value to <paramref name="backup"/>.
        /// </summary>
    }
}
