using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace RadeonSoftwareSlimmer.ViewModels
{
    /// <summary>
    /// PROTOTYPE ONLY. Drives <see cref="Views.MonitorPrototypeWindow"/> — the standalone test window
    /// where the new black-glass visual language is being developed before it touches the rest of the app.
    /// It is deliberately self-contained: it shares no state with the real <c>InstallMonitor</c> and can be
    /// driven entirely from sample data / the in-window playground controls.
    /// </summary>
    public sealed class MonitorPrototypeViewModel : ObservableObject
    {
        public enum DemoState
        {
            Waiting,
            Preparing,
            DetectingGpu,
            Downloading,
            InstallerOpen,
            InstallingDriver,
            ComponentComplete,
            Verifying,
            ApplyingServiceChanges,
            ApplyingTweaks,
            Complete,
            Warning,
            Error,
        }

        /// <summary>Colour/emphasis family for the hero status dot and progress accent.</summary>
        public enum StatusKind
        {
            Neutral,
            Active,
            Success,
            Caution,
            Danger,
        }

        private DemoState _state = DemoState.Waiting;
        private string _phaseLabel = "Waiting to start";
        private string _detail = "Launch the AMD installer to begin. This window follows every stage until your changes are applied.";
        private string _activityTag = "";
        private double _percent;
        private bool _indeterminate;
        private StatusKind _status = StatusKind.Neutral;
        private string _primaryActionText = "";
        private bool _showPrimaryAction;
        private bool _showSkipTweaks;
        private bool _isFinished;
        private CancellationTokenSource _sim;

        public MonitorPrototypeViewModel()
        {
            PlannedServiceChanges = new ObservableCollection<string>
            {
                "Disable service: AMD Crash Defender",
                "Disable service: AMD External Events Utility",
                "Disable task: StartCN",
                "Disable task: AMD Install Launcher",
            };

            PlannedGpuTweaks = new ObservableCollection<string>
            {
                "Enable Hardware-Accelerated GPU Scheduling",
                "Disable ULPS (multi-GPU power state)",
                "Block Windows Update from replacing this driver",
                "Turn off AMD user-experience telemetry",
            };
        }

        // ----- bound state -----

        public DemoState State
        {
            get => _state;
            private set { if (Set(ref _state, value)) Raise(nameof(StateName)); }
        }

        public string StateName => _state.ToString();

        public string PhaseLabel
        {
            get => _phaseLabel;
            private set => Set(ref _phaseLabel, value);
        }

        public string Detail
        {
            get => _detail;
            private set => Set(ref _detail, value);
        }

        /// <summary>
        /// Short at-a-glance category of what the installer is doing right now ("Downloading",
        /// "Installing", "Applying changes"). Blank when there is nothing specific to show. The real
        /// monitor gets this from the installer window text, not from a percentage.
        /// </summary>
        public string ActivityTag
        {
            get => _activityTag;
            private set => Set(ref _activityTag, value);
        }

        /// <summary>0..100. Drives the bar fill only — never shown as a number.</summary>
        public double Percent
        {
            get => _percent;
            private set => Set(ref _percent, Math.Max(0, Math.Min(100, value)));
        }

        public bool Indeterminate
        {
            get => _indeterminate;
            private set => Set(ref _indeterminate, value);
        }

        public StatusKind Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        public string PrimaryActionText
        {
            get => _primaryActionText;
            private set => Set(ref _primaryActionText, value);
        }

        public bool ShowPrimaryAction
        {
            get => _showPrimaryAction;
            private set => Set(ref _showPrimaryAction, value);
        }

        public bool ShowSkipTweaks
        {
            get => _showSkipTweaks;
            private set => Set(ref _showSkipTweaks, value);
        }

        public bool IsFinished
        {
            get => _isFinished;
            private set => Set(ref _isFinished, value);
        }

        public ObservableCollection<string> PlannedServiceChanges { get; }
        public ObservableCollection<string> PlannedGpuTweaks { get; }

        // ----- playground -----

        /// <summary>Jump straight to one demo state (used by the prototype control strip).</summary>
        public void ShowState(DemoState state)
        {
            CancelSimulation();
            Apply(state);
        }

        public void Reset()
        {
            CancelSimulation();
            Percent = 0;
            Apply(DemoState.Waiting);
        }

        public void SkipTweaks()
        {
            PlannedGpuTweaks.Clear();
            ShowSkipTweaks = false;
            Detail = "GPU tweaks skipped — they will not be applied after the install.";
        }

        /// <summary>Runs the full stage sequence on a timer so every transition can be watched.</summary>
        public async Task PlaySimulationAsync()
        {
            CancelSimulation();
            _sim = new CancellationTokenSource();
            CancellationToken ct = _sim.Token;

            try
            {
                Percent = 0;
                Apply(DemoState.Preparing);
                await Step(ct, 1400);

                Apply(DemoState.DetectingGpu);
                await Step(ct, 1500);

                Apply(DemoState.InstallerOpen);
                await Step(ct, 1800);

                Apply(DemoState.Downloading);
                for (int p = 0; p <= 100 && !ct.IsCancellationRequested; p += 4)
                {
                    Percent = p;
                    await Step(ct, 95);
                }

                Apply(DemoState.InstallingDriver);
                for (int p = 0; p <= 100 && !ct.IsCancellationRequested; p += 3)
                {
                    Percent = p;
                    await Step(ct, 115);
                }

                Apply(DemoState.ComponentComplete);
                await Step(ct, 1600);

                Apply(DemoState.Verifying);
                await Step(ct, 1700);

                Apply(DemoState.ApplyingServiceChanges);
                await Step(ct, 2000);

                Apply(DemoState.ApplyingTweaks);
                await Step(ct, 2000);

                Apply(DemoState.Complete);
            }
            catch (OperationCanceledException)
            {
                // playground reset / another state chosen — nothing to do
            }
        }

        private static async Task Step(CancellationToken ct, int ms)
        {
            await Task.Delay(ms, ct).ConfigureAwait(true);
        }

        private void CancelSimulation()
        {
            try { _sim?.Cancel(); } catch { /* ignore */ }
            _sim?.Dispose();
            _sim = null;
        }

        // ----- state → presentation -----

        private void Apply(DemoState state)
        {
            State = state;
            IsFinished = state == DemoState.Complete;
            ShowSkipTweaks = false;
            ShowPrimaryAction = false;
            PrimaryActionText = "";

            switch (state)
            {
                case DemoState.Waiting:
                    Set("Waiting to start",
                        "Launch the AMD installer to begin. This window follows every stage until your changes are applied.",
                        StatusKind.Neutral, indeterminate: false, tag: "", percent: 0);
                    break;

                case DemoState.Preparing:
                    Set("Preparing the installer",
                        "Unpacking the AMD Software package and checking your system.",
                        StatusKind.Active, indeterminate: true, tag: "Preparing");
                    break;

                case DemoState.DetectingGpu:
                    Set("Checking your graphics hardware",
                        "Detected AMD Radeon RX 7700 XT. Reading the driver that's installed now.",
                        StatusKind.Active, indeterminate: true, tag: "Detecting");
                    ShowSkipTweaks = true;
                    break;

                case DemoState.InstallerOpen:
                    Set("AMD installer is open",
                        "Choose Express Installation in the AMD installer window to begin.",
                        StatusKind.Active, indeterminate: true, tag: "Waiting for you");
                    ShowSkipTweaks = true;
                    break;

                case DemoState.Downloading:
                    Set("Downloading and extracting packages",
                        "AMD is downloading the driver package set and unpacking it. This can take a few minutes on a slow connection.",
                        StatusKind.Active, indeterminate: false, tag: "Downloading", percent: 35);
                    ShowSkipTweaks = true;
                    break;

                case DemoState.InstallingDriver:
                    Set("Installing the display driver",
                        "The screen may flicker, go black, or change resolution while the driver is replaced. This is normal.",
                        StatusKind.Active, indeterminate: false, tag: "Installing", percent: 68);
                    ShowSkipTweaks = true;
                    break;

                case DemoState.ComponentComplete:
                    Set("GPU driver finished installing",
                        "Select Close in the AMD installer window to finish. Your queued changes run as soon as it closes.",
                        StatusKind.Success, indeterminate: false, tag: "Almost done", percent: 100);
                    break;

                case DemoState.Verifying:
                    Set("Verifying the installed driver",
                        "Confirming the new driver is in place before applying your changes.",
                        StatusKind.Active, indeterminate: true, tag: "Verifying");
                    break;

                case DemoState.ApplyingServiceChanges:
                    Set("Applying service and task changes",
                        "Disabling the AMD background services and scheduled tasks you turned off.",
                        StatusKind.Active, indeterminate: true, tag: "Applying changes");
                    break;

                case DemoState.ApplyingTweaks:
                    Set("Applying GPU tweaks",
                        "Writing the registry tweaks you queued. Each value is backed up first so it can be reverted.",
                        StatusKind.Active, indeterminate: true, tag: "Applying changes");
                    break;

                case DemoState.Complete:
                    Set("All done",
                        "The driver is installed and your changes are applied. Restart when convenient, then select Close.",
                        StatusKind.Success, indeterminate: false, tag: "", percent: 100);
                    PrimaryActionText = "Finish and Export";
                    ShowPrimaryAction = true;
                    break;

                case DemoState.Warning:
                    Set("Couldn't confirm the install",
                        "The AMD installer closed before this app could confirm the result — normal when you reinstall the "
                        + "same version. If it finished, apply your queued changes now.",
                        StatusKind.Caution, indeterminate: false, tag: "", percent: 0);
                    PrimaryActionText = "Apply my changes now";
                    ShowPrimaryAction = true;
                    break;

                case DemoState.Error:
                    Set("Installation stopped",
                        "The AMD installer exited before the driver was replaced. No service or registry changes were made.",
                        StatusKind.Danger, indeterminate: false, tag: "", percent: 0);
                    PrimaryActionText = "Apply my changes now";
                    ShowPrimaryAction = true;
                    break;
            }
        }

        private void Set(string label, string detail, StatusKind status, bool indeterminate, string tag, double? percent = null)
        {
            PhaseLabel = label;
            Detail = detail;
            Status = status;
            Indeterminate = indeterminate;
            ActivityTag = tag ?? "";
            if (percent.HasValue)
                Percent = percent.Value;
        }
    }
}
