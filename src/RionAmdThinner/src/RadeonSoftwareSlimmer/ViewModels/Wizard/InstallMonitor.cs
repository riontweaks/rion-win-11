using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public enum InstallPhase
    {
        NotStarted,
        Starting,
        Downloading,
        InstallerOpen,
        InstallingDriver,
        Finishing,
        Verifying,
        NoChangeDetected,
        ApplyingServiceChanges,
        ApplyingGpuTweaks,
        Complete,
        Failed,
    }

    /// <summary>Colour / emphasis family for the monitor's status dot.</summary>
    public enum MonitorStatus
    {
        Neutral,
        Active,
        Success,
        Caution,
        Danger,
    }

    /// <summary>
    /// Shared, observable state for the monitored install. The Install &amp; Verify step drives it;
    /// the pop-out Installation Monitor window shows a minimal view of it: current phase, a progress
    /// bar, and the list of changes queued to apply once the driver install finishes.
    /// </summary>
    public sealed class InstallMonitor : ObservableObject
    {
        private InstallPhase _phase = InstallPhase.NotStarted;
        private string _detail = "The installer has not been launched yet.";
        private bool _applyGpuTweaks;
        private bool _gpuTweaksCancelled;
        private double _percent;
        private bool _hasPercent;

        public bool ApplyGpuTweaks
        {
            get => _applyGpuTweaks;
            set => Set(ref _applyGpuTweaks, value);
        }

        public InstallPhase Phase
        {
            get => _phase;
            private set
            {
                if (Set(ref _phase, value))
                {
                    Raise(nameof(PhaseLabel));
                    Raise(nameof(ActivityTag));
                    Raise(nameof(Status));
                    Raise(nameof(IsRunning));
                    Raise(nameof(IsFinished));
                    Raise(nameof(ProgressIndeterminate));
                    Raise(nameof(CanCancelGpuTweaks));
                }
            }
        }

        public string PhaseLabel
        {
            get
            {
                switch (_phase)
                {
                    case InstallPhase.Starting: return "Starting the AMD installer";
                    case InstallPhase.Downloading: return "Downloading and extracting packages";
                    case InstallPhase.InstallerOpen: return "AMD installer is open";
                    case InstallPhase.InstallingDriver: return "Installing driver packages";
                    case InstallPhase.Finishing: return "Finishing up";
                    case InstallPhase.Verifying: return "Verifying the installed driver";
                    case InstallPhase.NoChangeDetected: return "Couldn't confirm the install";
                    case InstallPhase.ApplyingServiceChanges: return "Applying service and task changes";
                    case InstallPhase.ApplyingGpuTweaks: return "Applying GPU tweaks";
                    case InstallPhase.Complete: return "All done";
                    case InstallPhase.Failed: return "Stopped - see the details";
                    default: return "Waiting to start";
                }
            }
        }

        /// <summary>Short at-a-glance category shown above the progress bar (no exact %/ETA).</summary>
        public string ActivityTag
        {
            get
            {
                switch (_phase)
                {
                    case InstallPhase.Starting: return "Preparing";
                    case InstallPhase.Downloading: return "Downloading";
                    case InstallPhase.InstallerOpen: return "Waiting for you";
                    case InstallPhase.InstallingDriver: return "Installing";
                    case InstallPhase.Finishing: return "Finishing";
                    case InstallPhase.Verifying: return "Verifying";
                    case InstallPhase.ApplyingServiceChanges:
                    case InstallPhase.ApplyingGpuTweaks: return "Applying changes";
                    default: return "";
                }
            }
        }

        public MonitorStatus Status
        {
            get
            {
                switch (_phase)
                {
                    case InstallPhase.Complete: return MonitorStatus.Success;
                    case InstallPhase.Failed: return MonitorStatus.Danger;
                    case InstallPhase.NoChangeDetected: return MonitorStatus.Caution;
                    case InstallPhase.NotStarted: return MonitorStatus.Neutral;
                    default: return MonitorStatus.Active;
                }
            }
        }

        public string Detail
        {
            get => _detail;
            private set => Set(ref _detail, value);
        }

        /// <summary>0..100 progress, when the AMD installer reports it.</summary>
        public double Percent
        {
            get => _percent;
            private set => Set(ref _percent, value);
        }

        public bool ProgressIndeterminate => IsRunning && !_hasPercent;

        public bool IsRunning =>
            _phase != InstallPhase.NotStarted
            && _phase != InstallPhase.Complete
            && _phase != InstallPhase.Failed
            && _phase != InstallPhase.NoChangeDetected;

        public bool IsFinished => _phase == InstallPhase.Complete || _phase == InstallPhase.Failed;

        private bool IsBeforeApply =>
            _phase == InstallPhase.Starting || _phase == InstallPhase.Downloading
            || _phase == InstallPhase.InstallerOpen || _phase == InstallPhase.InstallingDriver
            || _phase == InstallPhase.Finishing || _phase == InstallPhase.Verifying;

        /// <summary>True while the user can still call off the queued GPU tweaks (installer still running).</summary>
        public bool CanCancelGpuTweaks => IsBeforeApply && !_gpuTweaksCancelled && PlannedGpuTweaks.Count > 0;

        public bool GpuTweaksCancelled => _gpuTweaksCancelled;

        /// <summary>Called from the monitor window while the installer runs - the queued GPU tweaks won't be applied.</summary>
        public void CancelGpuTweaks()
        {
            if (_gpuTweaksCancelled)
                return;
            _gpuTweaksCancelled = true;
            ApplyGpuTweaks = false;
            PlannedGpuTweaks.Clear();
            Raise(nameof(CanCancelGpuTweaks));
            Detail = "GPU tweaks cancelled - they will not be applied after the install.";
            AddLog("GPU tweaks cancelled by the user - they will not be applied after the install.");
        }

        /// <summary>Post-install service / scheduled-task changes queued for after a verified install.</summary>
        public ObservableCollection<string> PlannedServiceChanges { get; } = new ObservableCollection<string>();

        /// <summary>GPU registry tweaks queued for after a verified install (frozen at launch).</summary>
        public ObservableCollection<string> PlannedGpuTweaks { get; } = new ObservableCollection<string>();

        /// <summary>What actually happened (not shown in the minimal window, kept for the audit summary).</summary>
        public ObservableCollection<string> AppliedActions { get; } = new ObservableCollection<string>();

        public void SetPhase(InstallPhase phase, string detail, double? percent = null)
        {
            Phase = phase;
            Detail = detail;

            if (percent.HasValue)
            {
                _hasPercent = true;
                Percent = Math.Max(0, Math.Min(100, percent.Value));
            }
            else if (phase == InstallPhase.Complete)
            {
                _hasPercent = true;
                Percent = 100;
            }
            else if (phase == InstallPhase.Failed || phase == InstallPhase.NoChangeDetected)
            {
                _hasPercent = false;
                Percent = 0;
            }

            Raise(nameof(ProgressIndeterminate));
        }

        /// <summary>Freezes what will be applied after a verified install. Called once, at launch.</summary>
        public void FreezePlan(IEnumerable<string> serviceChanges, IEnumerable<string> gpuTweaks)
        {
            PlannedServiceChanges.Clear();
            foreach (string s in serviceChanges)
                PlannedServiceChanges.Add(s);

            PlannedGpuTweaks.Clear();
            foreach (string t in gpuTweaks)
                PlannedGpuTweaks.Add(t);

            AppliedActions.Clear();
            _gpuTweaksCancelled = false;
            _manualApply = null;
            AwaitingManualConfirm = false;
            Raise(nameof(CanCancelGpuTweaks));
            Raise(nameof(GpuTweaksCancelled));
            Raise(nameof(AwaitingManualConfirm));
        }

        public void RecordApplied(string what) => AppliedActions.Add(what);

        // --- manual "apply anyway" for when the install couldn't be auto-confirmed (same-version repair) ---

        private System.Func<System.Threading.Tasks.Task> _manualApply;

        public bool AwaitingManualConfirm { get; private set; }

        public void OfferManualApply(System.Func<System.Threading.Tasks.Task> apply)
        {
            _manualApply = apply;
            AwaitingManualConfirm = true;
            Raise(nameof(AwaitingManualConfirm));
        }

        public async System.Threading.Tasks.Task RunManualApplyAsync()
        {
            var apply = _manualApply;
            if (!AwaitingManualConfirm || apply == null)
                return;
            AwaitingManualConfirm = false;
            _manualApply = null;
            Raise(nameof(AwaitingManualConfirm));
            await apply();
        }

        /// <summary>Routes a line to the app's Activity Log (the minimal monitor window shows no log of its own).</summary>
        public void AddLog(string line) => RadeonSoftwareSlimmer.ViewModels.StaticViewModel.AddLogMessage(line);
    }
}
