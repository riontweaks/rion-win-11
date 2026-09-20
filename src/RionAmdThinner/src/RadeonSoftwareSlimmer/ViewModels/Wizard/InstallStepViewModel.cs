using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public sealed class InstallStepViewModel : WizardStepViewModel
    {
        private bool _launched;
        private CancellationTokenSource _cts;
        private List<GpuTweak> _frozenTweaks = new List<GpuTweak>();

        public InstallStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.Install, "Install and Verify", "")
        {
            LaunchCommand = new RelayCommand(Launch, () => !_launched);
            OpenFolderCommand = new RelayCommand(OpenFolder);
            OpenMonitorCommand = new RelayCommand(() => Wizard.RequestMonitorWindow());

            GpuTweaks = new ObservableCollection<GpuTweakRowViewModel>();
        }

        private InstallMonitor Monitor => Wizard.Monitor;
        private PostInstallStepViewModel Configure => (PostInstallStepViewModel)Wizard.StepFor(WizardStep.Configure);

        public HardwareInfo Hardware => Wizard.Hardware;
        public string TargetVersion => Wizard.InstallerInfo?.PackageVersion ?? "the selected package";
        public string CurrentVersion => Hardware.DriverVersion ?? "unknown";
        public string WorkspacePath => Wizard.Session.WorkspacePath;
        public bool IsElevated => Wizard.IsElevated;

        private string _status = "The AMD installer runs normally - nothing is silent. When it finishes, this app verifies the driver and applies the service/task changes you chose" +
                                 " and, if the box below is ticked, the GPU tweaks. Live progress opens in its own window.";
        public string Status { get => _status; private set => Set(ref _status, value); }

        public bool Launched
        {
            get => _launched;
            private set
            {
                Set(ref _launched, value);
                NotifyAdvanceChanged();
                Raise(nameof(CanGoBack));
                LaunchCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>The "Apply AMD GPU tweaks" master switch.</summary>
        public bool ShowGpuTweaks => EditionPolicy.HasAdvancedTools;

        public bool ApplyGpuTweaks
        {
            get => EditionPolicy.HasAdvancedTools && Monitor.ApplyGpuTweaks;
            set
            {
                Monitor.ApplyGpuTweaks = EditionPolicy.HasAdvancedTools && value;
                Raise(nameof(ApplyGpuTweaks));
                Raise(nameof(LaunchButtonText));
                RebuildTweakPreview();
            }
        }

        public string LaunchButtonText =>
            ApplyGpuTweaks && GpuTweaks.Any(t => t.WillApply)
                ? "Launch Installer with Tweaks"
                : "Launch AMD Installer";

        /// <summary>The selectable curated GPU registry tweaks.</summary>
        public ObservableCollection<GpuTweakRowViewModel> GpuTweaks { get; }

        public RelayCommand LaunchCommand { get; }
        public RelayCommand OpenFolderCommand { get; }
        public RelayCommand OpenMonitorCommand { get; }

        public override bool CanAdvance => Monitor.Phase == InstallPhase.Complete;
        public override bool CanGoBack => !_launched;
        public override string AdvanceLabel => "Finish and Export";

        public override void OnEnter()
        {
            if (EditionPolicy.HasAdvancedTools && GpuTweaks.Count == 0)
            {
                foreach (GpuTweak tweak in GpuTweakCatalog.All)
                {
                    var row = new GpuTweakRowViewModel(tweak, Wizard.GpuTweaks.IsApplied(tweak));
                    row.PropertyChanged += OnTweakRowChanged;
                    GpuTweaks.Add(row);
                }
                Monitor.ApplyGpuTweaks = Wizard.IsElevated && GpuTweaks.Any(t => t.WillApply);
                Raise(nameof(ApplyGpuTweaks));
                Raise(nameof(LaunchButtonText));
            }

            if (!_launched)
            {
                Configure.BuildPlan(Monitor);   // preview of the service/task changes
                RebuildTweakPreview();
            }
        }

        private void OnTweakRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(GpuTweakRowViewModel.Selected))
                return;
            Raise(nameof(LaunchButtonText));
            if (!_launched)
                RebuildTweakPreview();
        }

        private void RebuildTweakPreview()
        {
            Monitor.PlannedGpuTweaks.Clear();
            if (!ApplyGpuTweaks)
                return;
            foreach (GpuTweakRowViewModel row in GpuTweaks.Where(t => t.WillApply))
                Monitor.PlannedGpuTweaks.Add(row.Title);
        }

        private async void Launch()
        {
            try
            {
                _frozenTweaks = ApplyGpuTweaks
                    ? GpuTweaks.Where(t => t.WillApply).Select(t => t.Tweak).ToList()
                    : new List<GpuTweak>();

                Configure.BuildPlan(Monitor);
                Monitor.FreezePlan(
                    Monitor.PlannedServiceChanges.ToList(),
                    _frozenTweaks.Select(t => t.Title));

                Process process = Wizard.Pre.StartModifiedInstaller();
                if (process == null)
                {
                    Status = "Setup.exe was not found in the prepared folder. Go back to Review and prepare the installer again.";
                    return;
                }

                Wizard.Session.Status = SessionStatus.Installing;
                Launched = true;
                Monitor.SetPhase(InstallPhase.Starting, "Launching the AMD installer.");
                AuditLog.Action("Launch installer", "RunSetup", WorkspacePath,
                    newValue: _frozenTweaks.Count > 0 ? _frozenTweaks.Count + " GPU tweak(s) queued" : "no GPU tweaks",
                    reversible: false);
                Status = "AMD installer launched - watch the Installation Monitor window.";
                Wizard.RequestMonitorWindow();

                _cts = new CancellationTokenSource();
                var watcher = new InstallerWatcher();
                var progress = new Progress<WatchProgress>(p => Monitor.SetPhase(Map(p.Phase), p.Detail, p.Percent));

                InstallOutcome outcome;
                using (process)
                {
                    outcome = await watcher.WatchAsync(process, Wizard.Hardware, progress, _cts.Token).ConfigureAwait(true);
                }

                switch (outcome)
                {
                    case InstallOutcome.NeverStarted:
                        Monitor.SetPhase(InstallPhase.NoChangeDetected,
                            "The AMD installer never opened, so nothing was changed. Click Launch again once it is running.");
                        AuditLog.Action("Monitor install", "Verify", "installer", newValue: "never started", result: "Skipped", reversible: false);
                        Launched = false;
                        Status = "The installer did not start. Nothing was applied.";
                        return;

                    case InstallOutcome.ClosedNoChange:
                        Monitor.SetPhase(InstallPhase.NoChangeDetected,
                            "The AMD installer closed but this app couldn't confirm the install (normal when you reinstall the "
                            + "same version). If the AMD installer finished, click “Apply my changes now”.");
                        AuditLog.Action("Monitor install", "Verify", "installer", newValue: "closed, not auto-confirmed", result: "Manual", reversible: false);
                        Monitor.OfferManualApply(ApplyAfterInstallAsync);
                        Launched = false;   // also allowed to just launch again
                        Status = "Couldn't auto-confirm the install - use “Apply my changes now” in the monitor window, or launch again.";
                        return;

                    case InstallOutcome.Installed:
                    default:
                        await ApplyAfterInstallAsync().ConfigureAwait(true);
                        return;
                }
            }
            catch (Exception ex)
            {
                Monitor.SetPhase(InstallPhase.Failed, "Could not run or monitor the installer: " + ex.Message);
                Launched = false;
                Status = "Could not run or monitor the installer: " + ex.Message;
                AuditLog.Action("Launch installer", "RunSetup", WorkspacePath, result: "Error", reversible: false, error: ex.Message);
            }
        }

        private async Task ApplyAfterInstallAsync()
        {
            // Vendor-app launch and installer closure both lead here, after validation.
            // Never terminate an installer while it may still be writing packages.
            if (await Task.Run(InstallerWatcher.AmdInstallerStillRunning).ConfigureAwait(true))
            {
                Monitor.SetPhase(InstallPhase.Finishing,
                    "Driver validated. Close the AMD installer to continue with your selected changes.");
                while (await Task.Run(InstallerWatcher.AmdInstallerStillRunning).ConfigureAwait(true))
                    await Task.Delay(1000).ConfigureAwait(true);
            }

            Monitor.SetPhase(InstallPhase.Verifying, "Validating the installed AMD driver…");
            HardwareInfo fresh = await Task.Run(() => new HardwareInspector().Inspect("1002")).ConfigureAwait(true);
            if (!DriverValidation.IsReady(fresh, "1002"))
            {
                Monitor.SetPhase(InstallPhase.Failed, "AMD driver validation failed. Restart Windows if required, then retry. No post-install changes were applied.");
                Launched = false;
                Status = "Driver validation failed.";
                return;
            }
            AuditLog.Action("Auto-verify installation", "Verify", fresh.GpuName,
                oldValue: Wizard.Hardware?.DriverVersion, newValue: fresh.DriverVersion,
                result: "Passed", reversible: false);
            Wizard.Session.Status = SessionStatus.PostInstall;

            // 1. AMD service / scheduled-task selection
            int serviceChangeCount = Monitor.PlannedServiceChanges.Count;
            Monitor.SetPhase(InstallPhase.ApplyingServiceChanges,
                serviceChangeCount > 0
                    ? $"Applying {serviceChangeCount} AMD service/task change(s) you chose…"
                    : "No service or task changes were selected.");

            Task apply = Configure.ApplySelectedAsync(Monitor);
            if (await Task.WhenAny(apply, Task.Delay(TimeSpan.FromMinutes(3))).ConfigureAwait(true) != apply)
            {
                Monitor.AddLog("Service/task changes are taking longer than expected - continuing. Check the Post-Install page later.");
            }
            else
            {
                await apply.ConfigureAwait(true); // surface any exception
            }

            // 2. GPU registry tweaks (frozen at launch, unless the user cancelled them from the monitor)
            int tweakCount = 0;
            if (Monitor.GpuTweaksCancelled)
                _frozenTweaks.Clear();

            if (EditionPolicy.HasAdvancedTools && Monitor.ApplyGpuTweaks && !Monitor.GpuTweaksCancelled && _frozenTweaks.Count > 0)
            {
                Monitor.SetPhase(InstallPhase.ApplyingGpuTweaks, $"Applying {_frozenTweaks.Count} GPU tweak(s)…");
                var backup = new BackupSession { Description = "GPU tweaks after install (" + Wizard.Session.SessionId + ")" };
                IReadOnlyList<string> written = await Task.Run(() => Wizard.GpuTweaks.Apply(_frozenTweaks, backup)).ConfigureAwait(true);
                string savedTo = backup.Save();

                // Every queued tweak is now in its target state; some may have already been set.
                tweakCount = _frozenTweaks.Count;
                foreach (GpuTweak t in _frozenTweaks)
                    Monitor.RecordApplied("GPU tweak: " + t.Title + (written.Contains(t.Id) ? "" : " (already set)"));

                if (savedTo != null)
                    Monitor.AddLog("GPU tweak undo data saved to " + savedTo);
            }

            string tweakNote = Monitor.GpuTweaksCancelled
                ? "GPU tweaks were cancelled"
                : $"{tweakCount} GPU tweak(s)";

            Wizard.Session.Status = SessionStatus.Complete;
            Monitor.SetPhase(InstallPhase.Complete,
                $"Driver installed. Applied {serviceChangeCount} service/task change(s); {tweakNote}. "
                + "A restart is recommended - then continue to Finish.");
            Status = "Done - continue to Finish.";
            if (Wizard.Current == this && Wizard.NextCommand.CanExecute(null)) Wizard.NextCommand.Execute(null);
        }

        private static InstallPhase Map(WatchPhase phase)
        {
            switch (phase)
            {
                case WatchPhase.Starting: return InstallPhase.Starting;
                case WatchPhase.Downloading: return InstallPhase.Downloading;
                case WatchPhase.InstallerOpen: return InstallPhase.InstallerOpen;
                case WatchPhase.InstallingDriver: return InstallPhase.InstallingDriver;
                case WatchPhase.Finishing: return InstallPhase.Finishing;
                case WatchPhase.Verifying: return InstallPhase.Verifying;
                default: return InstallPhase.InstallerOpen;
            }
        }

        private void OpenFolder()
        {
            try { Process.Start(new ProcessStartInfo(WorkspacePath) { UseShellExecute = true }); }
            catch (Exception ex) { Status = "Could not open the folder: " + ex.Message; }
        }
    }
}

