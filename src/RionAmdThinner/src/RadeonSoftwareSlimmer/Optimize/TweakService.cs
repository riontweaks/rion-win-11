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
    public enum TweakState
    {
        /// <summary>Current value already equals the target — nothing to do.</summary>
        AtTarget,
        /// <summary>Missing, at the Windows default, or set to some other value — will be written.</summary>
        NeedsApply,
        /// <summary>Could not be read.</summary>
        Unreadable,
    }

    /// <summary>
    /// Reads and applies <see cref="SystemTweak"/> entries — against the real registry (HKLM or
    /// HKCU) for most, or via a shell command for the handful with no registry representation
    /// (<see cref="SystemTweak.IsCommandBased"/>). Generalisation of <see cref="GpuTweakService"/>:
    /// every write captures the previous value to the backup store first and is recorded in the
    /// audit log; a tweak already at its target value is skipped.
    /// </summary>
    public sealed class TweakService
    {
        private readonly IRegistry _registry;
        private readonly Func<bool> _isElevated;

        public TweakService(IRegistry registry)
        {
            _registry = registry;
            _isElevated = () => Elevation.IsElevated;
        }

        internal TweakService(IRegistry registry, Func<bool> isElevated)
        {
            _registry = registry;
            _isElevated = isElevated;
        }

        private IRegistryKey Root(SystemTweak t) =>
            t.Hive == TweakHive.CurrentUser ? _registry.CurrentUser : _registry.LocalMachine;

        private static string PathOf(SystemTweak t) =>
            t.DynamicKeyPath != null ? t.DynamicKeyPath() : t.KeyPath;

        /// <summary>Converts a value read back from the registry to a comparable long, the same
        /// way it was written by ApplyOne. A plain Convert.ToInt64 sign-extends a DWord whose top
        /// bit is set (e.g. 0xFFFFFFFF, written as the signed int32 -1) to -1 instead of
        /// 4294967295 — which made every tweak using a top-bit-set target (NetworkThrottlingIndex
        /// = 0xFFFFFFFF, used by both "Disable Network Throttling" tweaks) never compare equal to
        /// its own target after being applied, always reporting NeedsApply even once correctly set.</summary>
        private static long NormalizeValue(object v, RegistryValueKind kind) =>
            kind == RegistryValueKind.DWord && v is int i ? unchecked((long)(uint)i) : Convert.ToInt64(v, CultureInfo.InvariantCulture);

        private static object RegistryTarget(SystemTweak tweak, long target) =>
            tweak.BinaryTargetValue != null ? tweak.BinaryTargetValue : tweak.ValueKind == RegistryValueKind.QWord
                ? (object)target : tweak.ValueKind == RegistryValueKind.String ? (tweak.StringValues != null ? tweak.StringValues[target] : target.ToString(CultureInfo.InvariantCulture))
                : (object)unchecked((int)target);

        public TweakState Inspect(SystemTweak tweak, out long? current) => Inspect(tweak, null, out current);

        public TweakState InspectWithError(SystemTweak tweak, out long? current, out string error) =>
            Inspect(tweak, null, out current, out error);

        private TweakState Inspect(SystemTweak tweak, long? requestedTarget, out long? current) =>
            Inspect(tweak, requestedTarget, out current, out _);

        private TweakState Inspect(SystemTweak tweak, long? requestedTarget, out long? current, out string error)
        {
            current = null;
            error = null;

            try
            {
                if (tweak.IsCommandBased || tweak.IsCustom)
                {
                    bool? applied = tweak.InspectCommandState?.Invoke();
                    if (applied == null)
                    {
                        error = "Windows did not return a readable configuration for this setting.";
                        return TweakState.Unreadable;
                    }
                    return applied.Value ? TweakState.AtTarget : TweakState.NeedsApply;
                }
                string path = PathOf(tweak);
                if (string.IsNullOrEmpty(path))
                    return TweakState.Unreadable;

                using (IRegistryKey key = Root(tweak).OpenSubKey(path, false))
                {
                    var actual = RegistryValueSnapshot.Read(key, tweak.ValueName);
                    if (actual.Exists && (actual.Kind == RegistryValueKind.DWord || actual.Kind == RegistryValueKind.QWord))
                        current = NormalizeValue(actual.Value(), actual.Kind);
                    else if (actual.Exists && actual.Kind == RegistryValueKind.String && long.TryParse(actual.Value() as string,
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out long numeric)) current = numeric;
                    if (actual.Exists && actual.Kind == RegistryValueKind.String && tweak.StringValues != null)
                    {
                        foreach (var choice in tweak.StringValues)
                            if (string.Equals(choice.Value, actual.Value() as string, StringComparison.Ordinal)) current = choice.Key;
                    }
                    var requested = RegistryValueSnapshot.Capture(!tweak.DeleteRegistryValue,
                        RegistryTarget(tweak, requestedTarget ?? tweak.EffectiveTarget), tweak.ValueKind);
                    return actual.Same(requested) ? TweakState.AtTarget : TweakState.NeedsApply;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                StaticViewModel.AddDebugMessage(ex, "Could not read tweak " + tweak.Id);
                return TweakState.Unreadable;
            }
        }

        public bool IsApplied(SystemTweak tweak) => Inspect(tweak, out _) == TweakState.AtTarget;

        /// <summary>
        /// Applies every tweak whose current value differs from its target. Returns the ids that
        /// were actually written; sets <paramref name="anyRebootNeeded"/> if any written tweak
        /// needs a restart. Each write's previous value goes to <paramref name="backup"/>.
        /// </summary>
        public IReadOnlyList<string> Apply(IEnumerable<SystemTweak> tweaks, BackupSession backup, out bool anyRebootNeeded)
        {
            var results = tweaks.Select(t => ApplyDetailed(t, backup)).ToList();
            anyRebootNeeded = results.Any(r => r.Status == TweakOperationStatus.Applied && r.RebootRequired);
            return results.Where(r => r.Status == TweakOperationStatus.Applied).Select(r => r.Id).ToList();
        }

        // A card is one operation: preflight every member, verify every target, and undo
        // writes from this attempt if a member fails. Existing target values are untouched.
        public IReadOnlyList<TweakOperationResult> RunGroup(IReadOnlyList<SystemTweak> tweaks, bool applying) =>
            RunGroup(tweaks, applying, BackupStore.BackupDirectory);

        internal IReadOnlyList<TweakOperationResult> RunGroup(IReadOnlyList<SystemTweak> tweaks, bool applying, string directory)
        {
            lock (RegistryChangeHistory.Sync)
            {
                var results = new List<TweakOperationResult>();
                if (applying)
                {
                    foreach (var tweak in tweaks)
                    {
                        string problem = tweak.ApplyBlockedReason;
                        try
                        {
                            if (string.IsNullOrEmpty(problem)) problem = tweak.CheckApplySupport?.Invoke(_registry);
                            if (string.IsNullOrEmpty(problem) && InspectWithError(tweak, out _, out var error) == TweakState.Unreadable)
                                problem = error ?? "Current state is unavailable.";
                        }
                        catch (Exception ex) { problem = ex.Message; }
                        if (!string.IsNullOrEmpty(problem))
                            results.Add(TweakOperationResult.ForAction(tweak.Id, tweak.Title, false, problem + " No card settings were changed."));
                    }
                    if (results.Count > 0) return results;
                }
                var previousRecords = RegistryChangeHistory.ListEntries(directory, _registry).Select(e => e.Id).ToHashSet();
                var backup = new BackupSession(directory) { Description = "Windows tweak card" };
                var changed = new List<SystemTweak>();
                foreach (var tweak in tweaks)
                {
                    var result = applying ? ApplyDetailed(tweak, backup) : RevertDetailed(tweak, directory);
                    results.Add(result);
                    if (applying && result.Status == TweakOperationStatus.Applied) changed.Add(tweak);
                    if (applying && !result.Success)
                    {
                        // A failed readback may follow a successful write. Its journal is also
                        // eligible for conflict-checked recovery; never report this as success.
                        if (result.Status == TweakOperationStatus.Failed) changed.Add(tweak);
                        foreach (var entry in RegistryChangeHistory.ListEntries(directory, _registry).Where(e => !previousRecords.Contains(e.Id)).Reverse())
                        {
                            var recovery = RegistryChangeHistory.Restore(entry.Id, _registry, directory);
                            if (!recovery.Success) results.Add(TweakOperationResult.ForAction(tweak.Id, entry.Target, false,
                                "Recovery needs attention: " + recovery.Message));
                        }
                        foreach (var prior in changed.Where(t => t.IsCustom || t.IsCommandBased).AsEnumerable().Reverse())
                        {
                            var recovery = RevertDetailed(prior, directory);
                            if (!recovery.Success) results.Add(TweakOperationResult.ForAction(prior.Id, prior.Title, false,
                                "Recovery needs attention: " + recovery.Message));
                        }
                        return results;
                    }
                }
                return results;
            }
        }

        public TweakOperationResult ApplyDetailed(SystemTweak tweak, BackupSession backup) =>
            ApplyDetailed(tweak, backup, tweak.EffectiveTarget);

        private TweakOperationResult ApplyDetailed(SystemTweak tweak, BackupSession backup, long target)
        {
            var result = new TweakOperationResult { Id = tweak.Id, Title = tweak.Title, RebootRequired = tweak.RebootRequired };
            try
            {
                if (!string.IsNullOrWhiteSpace(tweak.ApplyBlockedReason))
                    return result.Finish(TweakOperationStatus.Unsupported, tweak.ApplyBlockedReason);
                if (tweak.IsAdjustable && !tweak.Presets.Any(p => p.Value == target))
                    return result.Finish(TweakOperationStatus.Unsupported, "Select one of this setting's supported choices.");
                string unsupported = tweak.CheckApplySupport?.Invoke(_registry);
                if (!string.IsNullOrWhiteSpace(unsupported))
                    return result.Finish(TweakOperationStatus.Unsupported, unsupported);
                var state = Inspect(tweak, target, out var before, out var inspectionError);
                bool registry = !tweak.IsCommandBased && !tweak.IsCustom;
                result.PreviousValue = registry ? before?.ToString(CultureInfo.InvariantCulture) ?? RegistryDisplay(tweak) : state.ToString();
                result.RequestedValue = registry ? (tweak.DeleteRegistryValue || tweak.BinaryTargetValue != null ? tweak.TargetLabel : target.ToString(CultureInfo.InvariantCulture)) : "Enabled";
                if (state == TweakState.Unreadable)
                    return result.Finish(TweakOperationStatus.Unavailable,
                        (inspectionError ?? "Current state could not be read.") + " No change was attempted.");
                if (state == TweakState.AtTarget)
                    return result.Finish(TweakOperationStatus.AlreadyConfigured, "Already configured; no change was needed.");
                if (!_isElevated())
                    return result.Finish(TweakOperationStatus.Unavailable, "Administrator access is required; no change was attempted.");

                bool changed = tweak.IsCustom ? ApplyCustomOne(tweak) : tweak.IsCommandBased
                    ? ApplyCommandOne(tweak) : ApplyOne(tweak, target, backup);
                if (!changed)
                    return result.Finish(TweakOperationStatus.Failed, "The operation did not confirm a change. Review the log and saved recovery records.");
                var afterState = Inspect(tweak, target, out _);
                if (afterState != TweakState.AtTarget)
                    return result.Finish(TweakOperationStatus.Failed, "The requested value was not verified. A partial change may remain; review the setting’s Revert changes action.");
                return result.Finish(TweakOperationStatus.Applied, registry
                    ? result.PreviousValue + " -> " + result.RequestedValue + (tweak.RebootRequired ? "; restart required." : "; registry value verified.") + " " + tweak.VerificationNote
                    : "Requested state verified." + (tweak.RebootRequired ? " Restart required." : ""));
            }
            catch (Exception ex)
            {
                StaticViewModel.AddLogMessage(ex, "Could not apply tweak " + tweak.Id);
                return result.Finish(TweakOperationStatus.Failed, ex.Message);
            }
        }

        public bool ApplyValue(SystemTweak tweak, long value, BackupSession backup)
        {
            if (tweak.IsCommandBased || tweak.IsCustom) throw new InvalidOperationException("This setting has no registry preset.");
            var result = ApplyDetailed(tweak, backup, value);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            return result.Status == TweakOperationStatus.Applied;
        }

        private string RegistryDisplay(SystemTweak tweak)
        {
            string path = PathOf(tweak);
            if (string.IsNullOrEmpty(path)) return "Unavailable";
            using (var key = Root(tweak).OpenSubKey(path, false))
                return RegistryValueSnapshot.Read(key, tweak.ValueName).Display();
        }

        /// <summary>Restores a saved registry value exactly; never substitutes a guessed default.</summary>
        public bool Revert(SystemTweak tweak, BackupSession backup)
        {
            var result = RevertDetailed(tweak);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            return result.Status == TweakOperationStatus.Restored;
        }

        public TweakOperationResult RevertDetailed(SystemTweak tweak) => RevertDetailed(tweak, BackupStore.BackupDirectory);

        private TweakOperationResult RevertDetailed(SystemTweak tweak, string directory)
        {
            var result = new TweakOperationResult { Id = tweak.Id, Title = tweak.Title, RebootRequired = tweak.RebootRequired };
            try
            {
                if (!_isElevated()) return result.Finish(TweakOperationStatus.Unavailable, "Administrator access is required.");
                if (tweak.IsCustom || tweak.IsCommandBased)
                {
                    bool changed = tweak.IsCustom ? tweak.CustomRevert?.Invoke() == true
                        : !string.IsNullOrWhiteSpace(tweak.RevertCommand) && CommandTweakHelper.RunCommand(tweak.RevertCommand);
                    if (!changed) return result.Finish(TweakOperationStatus.Failed, "Recovery was not confirmed. Review the saved state and log.");
                    if (tweak.IsCommandBased && Inspect(tweak, out _) != TweakState.NeedsApply)
                        return result.Finish(TweakOperationStatus.Failed, "Reset was not verified. Check the current setting and log.");
                    return result.Finish(TweakOperationStatus.Restored, tweak.IsCommandBased
                        ? "Legacy default reset verified; the original value was not saved."
                        : "Saved-state recovery completed.");
                }
                string path = PathOf(tweak);
                if (string.IsNullOrEmpty(path)) return result.Finish(TweakOperationStatus.Unavailable, "The setting path is unavailable.");
                string target = (tweak.Hive == TweakHive.CurrentUser ? "HKCU\\" : "HKLM\\") + path + "\\" + tweak.ValueName;
                var restored = RegistryChangeHistory.RestoreLatest(target, _registry, directory);
                return result.Finish(restored.Success ? TweakOperationStatus.Restored : TweakOperationStatus.Failed, restored.Message);
            }
            catch (Exception ex) { return result.Finish(TweakOperationStatus.Failed, ex.Message); }
        }

        private bool ApplyCustomOne(SystemTweak tweak)
        {
            TweakState state = Inspect(tweak, out _);
            if (state == TweakState.AtTarget) return false;

            bool ok = tweak.CustomApply();
            AuditLog.Action("Tweaks", "Tweak", tweak.Id, newValue: "applied",
                result: ok ? "OK" : "Error", reversible: tweak.CustomRevert != null);
            if (ok) StaticViewModel.AddLogMessage("Applied tweak: " + tweak.Title);
            return ok;
        }

        private bool ApplyCommandOne(SystemTweak tweak)
        {
            TweakState state = Inspect(tweak, out _);
            if (state == TweakState.AtTarget) return false;

            bool ok = CommandTweakHelper.RunCommand(tweak.ApplyCommand);
            AuditLog.Action("Tweaks", "Tweak", tweak.Id, newValue: "applied",
                result: ok ? "OK" : "Error", reversible: !string.IsNullOrEmpty(tweak.RevertCommand));
            if (ok) StaticViewModel.AddLogMessage("Applied tweak: " + tweak.Title);
            return ok;
        }

        private bool ApplyOne(SystemTweak tweak, long targetValue, BackupSession backup)
        {
            string path = PathOf(tweak);
            if (string.IsNullOrEmpty(path))
                return false;

            string hiveName = tweak.Hive == TweakHive.CurrentUser ? "HKCU" : "HKLM";
            string target = hiveName + "\\" + path;

            using (IRegistryKey key = Root(tweak).OpenSubKey(path, true) ?? Root(tweak).CreateSubKey(path))
            {
                if (key == null)
                    return false;

                object existing = key.GetValue(tweak.ValueName, null);
                bool existed = existing != null;
                RegistryValueKind existingKind = existed ? key.GetValueKind(tweak.ValueName) : tweak.ValueKind;

                object toWrite = RegistryTarget(tweak, targetValue);
                if (RegistryValueSnapshot.Read(key, tweak.ValueName).Same(
                    RegistryValueSnapshot.Capture(!tweak.DeleteRegistryValue, toWrite, tweak.ValueKind)))
                    return false; // already at target — nothing to write, nothing to back up
                RegistryChangeHistory.Write(backup, key, target, tweak.ValueName, !tweak.DeleteRegistryValue, toWrite, tweak.ValueKind);

                AuditLog.Action("Tweaks", "Tweak", tweak.Id,
                    oldValue: existed ? existing.ToString() : "(unset)",
                    newValue: toWrite.ToString(),
                    result: "OK", reversible: true);
                StaticViewModel.AddLogMessage("Applied tweak: " + tweak.Title);
                return true;
            }
        }
    }
}

