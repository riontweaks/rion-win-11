using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using RadeonSoftwareSlimmer.Adlx;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public sealed class WizardViewModel : ObservableObject
    {
        public WizardViewModel()
        {
            Adlx = new NullAdlxService();
            Session = new SessionPlan();
            AuditLog.StartSession(Session.SessionId);

            Hardware = new HardwareInspector().Inspect();
            Session.HardwareSnapshot = Hardware;

            Pre = new PreInstallViewModel(new FileSystem());
            Post = new PostInstallViewModel(new FileSystem(), new WindowsRegistry());
            Validator = new InstallerValidator();
            GpuTweaks = new GpuTweakService(new WindowsRegistry());
            Monitor = new InstallMonitor();
            PostInstallTool = new PostInstallToolViewModel(this);

            Steps = new ObservableCollection<WizardStepViewModel>
            {
                new WelcomeStepViewModel(this),
                new SelectDriverStepViewModel(this),
                new AnalyzeStepViewModel(this),
                new CustomizeStepViewModel(this),
                new ReviewStepViewModel(this),
                new PostInstallStepViewModel(this),
                new InstallStepViewModel(this),
                new FinishStepViewModel(this),
            };

            NextCommand = new RelayCommand(GoNext, () => Current != null && Current.CanAdvance);
            BackCommand = new RelayCommand(GoBack, () => Current != null && Current.CanGoBack);
            JumpCommand = new RelayCommand(p => JumpTo(p as WizardStepViewModel));
            ShowPostInstallToolCommand = new RelayCommand(ShowPostInstallTool, () => EditionPolicy.HasAdvancedTools);

            // Every step is reachable from the rail at any time - the user can jump to
            // "Choose Services & Tweaks" (or anywhere) without having selected anything first.
            foreach (WizardStepViewModel step in Steps)
                step.IsReachable = true;

            _current = Steps[0];
            Steps[0].IsCurrent = true;
            Steps[0].OnEnter();
        }

        public ObservableCollection<WizardStepViewModel> Steps { get; }

        public HardwareInfo Hardware { get; }
        public SessionPlan Session { get; }
        public IAdlxService Adlx { get; }
        public InstallerValidator Validator { get; }

        /// <summary>Applies the curated GPU registry tweaks after a verified install.</summary>
        public GpuTweakService GpuTweaks { get; }

        /// <summary>Shared state for the Install &amp; Verify step and the pop-out Installation Monitor window.</summary>
        public InstallMonitor Monitor { get; }

        /// <summary>Standalone "Post-Install" tool (rail section, outside the wizard flow).</summary>
        public PostInstallToolViewModel PostInstallTool { get; }

        public RelayCommand ShowPostInstallToolCommand { get; }

        private bool _toolActive;

        /// <summary>True while a rail tool (Post-Install) is shown instead of a wizard step.</summary>
        public bool IsToolActive => _toolActive;

        /// <summary>What the content host shows: the current wizard step, or a rail tool.</summary>
        public object ActiveContent => _toolActive ? (object)PostInstallTool : Current;

        public string ActiveTitle => _toolActive ? PostInstallTool.Title : Current?.Title;

        /// <summary>True while a wizard step is showing - gates the step header, Back/Next bar.</summary>
        public bool IsOnWizardStep => !_toolActive;

        public string PostInstallRailState => _toolActive ? "active" : null;

        private void ShowPostInstallTool()
        {
            if (EditionPolicy.IsFree) return;
            if (!_toolActive)
            {
                _toolActive = true;
                RaiseActiveSurface();
            }
            _ = PostInstallTool.EnsureLoadedAsync();
        }

        private void RaiseActiveSurface()
        {
            Raise(nameof(ActiveContent));
            Raise(nameof(ActiveTitle));
            Raise(nameof(IsOnWizardStep));
            Raise(nameof(IsToolActive));
            Raise(nameof(PostInstallRailState));
            foreach (WizardStepViewModel step in Steps)
                step.RaiseRailState();
            RefreshCommands();
        }

        /// <summary>Raised when the Install step wants the Installation Monitor window shown / brought forward.</summary>
        public event System.Action MonitorWindowRequested;

        public void RequestMonitorWindow() => MonitorWindowRequested?.Invoke();

        /// <summary>Advances from Install to Finish (used by the monitor window's Finish button).</summary>
        public void AdvanceFromInstall()
        {
            if (Current?.Step == WizardStep.Install && NextCommand.CanExecute(null))
                NextCommand.Execute(null);
        }

        /// <summary>RadeonSoftwareSlimmer's pre-install engine — extraction, package / task / component parsing.</summary>
        public PreInstallViewModel Pre { get; }

        /// <summary>RadeonSoftwareSlimmer's post-install engine — services, tasks, host processes, temp files.</summary>
        public PostInstallViewModel Post { get; }

        public DriverInstallerInfo InstallerInfo { get; set; }

        public bool IsElevated => Elevation.IsElevated;
        public RelayCommand RelaunchElevatedCommand { get; } =
            new RelayCommand(Elevation.RelaunchElevated, () => !Elevation.IsElevated);

        public RelayCommand NextCommand { get; }
        public RelayCommand BackCommand { get; }
        public RelayCommand JumpCommand { get; }

        private WizardStepViewModel _current;
        public WizardStepViewModel Current
        {
            get => _current;
            private set
            {
                bool wasTool = _toolActive;
                _toolActive = false;
                if (_current == value)
                {
                    if (wasTool) RaiseActiveSurface();
                    return;
                }
                _current?.OnLeave();
                if (_current != null) _current.IsCurrent = false;
                _current = value;
                _current.IsReachable = true;
                _current.IsCurrent = true;
                _current.OnEnter();
                Raise(nameof(Current));
                RaiseActiveSurface();
            }
        }

        public void RefreshCommands()
        {
            NextCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
        }

        public WizardStepViewModel StepFor(WizardStep step) => Steps.First(s => s.Step == step);

        private void GoNext()
        {
            if (!Current.CanAdvance) return;
            int i = Steps.IndexOf(Current);
            if (i < Steps.Count - 1)
            {
                Current.IsComplete = true;
                Current = Steps[i + 1];
            }
        }

        private void GoBack()
        {
            int i = Steps.IndexOf(Current);
            if (i > 0 && Current.CanGoBack) Current = Steps[i - 1];
        }

        /// <summary>Direct navigation via the rail. Every step is reachable at any time.</summary>
        public void JumpTo(WizardStepViewModel step)
        {
            if (step == null || (Current != null && !Current.CanGoBack && Current.Step != WizardStep.Welcome)) return;
            if (step.IsReachable || step.Step <= Current.Step)
                Current = step;
        }
    }
}
