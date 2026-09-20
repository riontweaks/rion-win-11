using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Intefaces;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Reads and applies <see cref="GpuTweak"/> entries against the real registry. Every write is
    /// captured to the backup store first (so "Revert last apply" can undo it) and recorded in the
    /// audit log. All targets are under HKLM, so applying requires elevation.
    /// </summary>
    public sealed class GpuTweakService
    {
        private const string GraphicsDriversKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string DwmKey = @"SOFTWARE\Microsoft\Windows\Dwm";
        private const string WindowsUpdatePolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        private const string PowerThrottlingKey = @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling";
        private const string AmdConfigKey = @"SOFTWARE\AMD\CN";

        private readonly IRegistry _registry;

        public GpuTweakService(IRegistry registry)
        {
            _registry = registry;
        }

        public string ResolveKeyPath(GpuTweak tweak)
        {
            switch (tweak.Location)
            {
                case GpuTweakLocation.GraphicsDrivers: return GraphicsDriversKey;
                case GpuTweakLocation.Dwm: return DwmKey;
                case GpuTweakLocation.WindowsUpdatePolicy: return WindowsUpdatePolicyKey;
                case GpuTweakLocation.PowerThrottling: return PowerThrottlingKey;
                case GpuTweakLocation.AmdConfig: return AmdConfigKey;
                case GpuTweakLocation.AmdAdapterClassKey: return AmdInventory.GetGpuClassKeyPath(_registry);
                default: return null;
            }
        }

        /// <summary>True when the tweak's value is already set to its applied value.</summary>
        public bool IsApplied(GpuTweak tweak)
        {
            try
            {
                string path = ResolveKeyPath(tweak);
                if (path == null)
                    return false;

                using (IRegistryKey key = _registry.LocalMachine.OpenSubKey(path, false))
                {
                    object current = key?.GetValue(tweak.ValueName, null);
                    if (current == null)
                        return false;

                    return Convert.ToInt64(current, CultureInfo.InvariantCulture) == tweak.AppliedValue;
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not read GPU tweak " + tweak.Id);
                return false;
            }
        }

        /// <summary>
        /// Applies the given tweaks. Returns the ids that were actually written (already-applied and
        /// failed ones are skipped). <paramref name="backup"/> receives an undo record per write.
        /// </summary>
        public IReadOnlyList<string> Apply(IEnumerable<GpuTweak> tweaks, BackupSession backup)
        {
            var applied = new List<string>();

            if (!Elevation.IsElevated)
            {
                StaticViewModel.AddLogMessage("GPU tweaks need administrator rights - skipped. Restart as administrator to apply them.");
                AuditLog.Action("Apply GPU tweaks", "GpuTweaks", "all",
                    newValue: "skipped - not elevated", result: "Skipped", reversible: true);
                return applied;
            }

            foreach (GpuTweak tweak in tweaks)
            {
                try
                {
                    if (ApplyOne(tweak, backup))
                        applied.Add(tweak.Id);
                }
                catch (Exception ex)
                {
                    StaticViewModel.AddLogMessage(ex, "Could not apply GPU tweak " + tweak.Id);
                    AuditLog.Action("Apply GPU tweaks", "GpuTweak", tweak.Id,
                        result: "Error", reversible: true, error: ex.Message);
                }
            }

            return applied;
        }

        private bool ApplyOne(GpuTweak tweak, BackupSession backup)
        {
            string path = ResolveKeyPath(tweak);
            if (path == null)
                return false;

            string target = "HKLM\\" + path;

            using (IRegistryKey key = _registry.LocalMachine.OpenSubKey(path, true)
                                      ?? _registry.LocalMachine.CreateSubKey(path))
            {
                if (key == null)
                    return false;

                object existing = key.GetValue(tweak.ValueName, null);
                bool existed = existing != null;

                if (existed && Convert.ToInt64(existing, CultureInfo.InvariantCulture) == tweak.AppliedValue)
                    return false; // already applied - nothing to write, nothing to back up

                RegistryValueKind existingKind = existed ? key.GetValueKind(tweak.ValueName) : tweak.ValueKind;

                object toWrite = tweak.ValueKind == RegistryValueKind.QWord
                    ? (object)tweak.AppliedValue
                    : (object)unchecked((int)tweak.AppliedValue);
                RegistryChangeHistory.Write(backup, key, target, tweak.ValueName, true, toWrite, tweak.ValueKind);

                AuditLog.Action("Apply GPU tweaks", "GpuTweak", tweak.Id,
                    oldValue: existed ? existing.ToString() : "(unset)",
                    newValue: tweak.AppliedValue.ToString(CultureInfo.InvariantCulture),
                    result: "OK", reversible: true);
                StaticViewModel.AddLogMessage($"Applied GPU tweak: {tweak.Title}");
                return true;
            }
        }
    }
}

