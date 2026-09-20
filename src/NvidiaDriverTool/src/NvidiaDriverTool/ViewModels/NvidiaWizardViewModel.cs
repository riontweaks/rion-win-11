using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;
using RadeonSoftwareSlimmer.ViewModels;
using NvidiaDriverTool.Services;

namespace NvidiaDriverTool.ViewModels
{
    /// <summary>
    /// Orchestrates the NVIDIA driver install wizard — the NVIDIA mirror of
    /// <c>RadeonSoftwareSlimmer.ViewModels.Wizard.WizardViewModel</c>. Shorter flow (no
    /// Post-Install rail tool yet — see <see cref="NvidiaWizardStep"/>'s doc comment) but same
    /// Next/Back/Jump navigation shape, copied rather than shared since the concrete step set
    /// differs, exactly like <c>WizardStepViewModel</c> was copied.
    /// </summary>
    public sealed class NvidiaWizardViewModel : ObservableObject
    {
        public NvidiaWizardViewModel()
        {
            Hardware = new HardwareInspector().Inspect("10DE");
            DownloadService = new NvidiaDriverDownloadService();
            Validator = new InstallerValidator();

            Steps = new ObservableCollection<NvidiaWizardStepViewModel>
            {
                new WelcomeStepViewModel(this),
                new SelectDriverStepViewModel(this),
                new AnalyzeStepViewModel(this),
                new CustomizeStepViewModel(this),
                new CustomizeStepViewModel(this, background: true),
                new ReviewStepViewModel(this),
                new InstallStepViewModel(this),
                new PostInstallStepViewModel(this),
                new FinishStepViewModel(this),
            };

            if (EditionPolicy.IsFree) Steps.Remove(Steps.First(s => s.Step == NvidiaWizardStep.PostInstall));

            NextCommand = new RelayCommand(GoNext, () => Current != null && Current.CanAdvance && Steps.IndexOf(Current) < Steps.Count - 1);
            BackCommand = new RelayCommand(GoBack, () => Current != null && Current.CanGoBack);
            JumpCommand = new RelayCommand(p => JumpTo(p as NvidiaWizardStepViewModel));

            foreach (NvidiaWizardStepViewModel step in Steps)
                step.IsReachable = step.Step <= NvidiaWizardStep.SelectDriver;

            _current = Steps[0];
            Steps[0].IsCurrent = true;
            Steps[0].OnEnter();
        }

        public ObservableCollection<NvidiaWizardStepViewModel> Steps { get; }
        public System.Func<object> PostInstallContentFactory { get; set; }
        public System.Action OpenPostInstall { get; set; }
        public System.Action ShowDriver { get; set; }
        public bool ShowAdvancedTools => EditionPolicy.HasAdvancedTools;
        public object Metrics=>null;
        public string MonitorStatus=>"";
        public void RefreshMonitor() => Raise(nameof(MonitorStatus));

        public void ModifyPackage()
        {
            if (((InstallStepViewModel)StepFor(NvidiaWizardStep.Install)).IsRunning) return;
            foreach (var step in Steps.Where(s => s.Step >= NvidiaWizardStep.Customize))
                step.IsComplete = false;
            Current = StepFor(string.IsNullOrEmpty(ExtractedPath)
                ? NvidiaWizardStep.SelectDriver : NvidiaWizardStep.Customize);
            ShowDriver?.Invoke();
        }

        /// <summary>Whatever GPU/driver hardware detection already exists — <c>VendorId</c>
        /// ("10DE" for NVIDIA, "1002" for AMD, per <c>HardwareInfo.IsAmdGpu</c>'s own check)
        /// and <c>GpuName</c> are already vendor-neutral, no changes needed to reuse them.</summary>
        public HardwareInfo Hardware { get; }
        public bool GpuIsNvidia => !string.IsNullOrEmpty(Hardware.VendorId)
            && Hardware.VendorId.Equals("10DE", System.StringComparison.OrdinalIgnoreCase);

        public NvidiaDriverDownloadService DownloadService { get; }
        public InstallerValidator Validator { get; }

        /// <summary>Set once a candidate installer (downloaded or browsed-to) is chosen.</summary>
        public DriverInstallerInfo InstallerInfo { get; set; }

        /// <summary>Set once the installer has been extracted — the folder Analyze/Customize/
        /// Install all operate on.</summary>
        public string ExtractedPath { get; set; }

        /// <summary>Parsed once in Analyze; edited (kept/excluded) in Customize.</summary>
        public IReadOnlyList<NvidiaComponentManifest.NvidiaComponent> Components { get; set; }
            = new List<NvidiaComponentManifest.NvidiaComponent>();

        /// <summary>Component names the user chose to exclude — everything else installs.</summary>
        public HashSet<string> ExcludedComponents { get; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public NvidiaInstallationOptions InstallationOptions { get; set; } = new();

        public RelayCommand NextCommand { get; }
        public RelayCommand BackCommand { get; }
        public RelayCommand JumpCommand { get; }

        private NvidiaWizardStepViewModel _current;
        public NvidiaWizardStepViewModel Current
        {
            get => _current;
            private set
            {
                if (_current == value) return;
                _current?.OnLeave();
                if (_current != null) _current.IsCurrent = false;
                _current = value;
                _current.IsReachable = true;
                _current.IsCurrent = true;
                _current.OnEnter();
                Raise(nameof(Current));
                Raise(nameof(HeaderTitle));
                RefreshCommands();
            }
        }

        public string HeaderTitle => Current?.Title;

        public void ResetPackage()
        {
            ExtractedPath = null;
            Components = new List<NvidiaComponentManifest.NvidiaComponent>();
            ExcludedComponents.Clear();
            InstallationOptions = new();
            foreach (var step in Steps.Where(s => s.Step >= NvidiaWizardStep.Analyze))
            {
                step.IsComplete = false;
                step.IsReachable = false;
            }
            ((AnalyzeStepViewModel)StepFor(NvidiaWizardStep.Analyze)).Reset();
        }

        public void RefreshCommands()
        {
            NextCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
        }

        public NvidiaWizardStepViewModel StepFor(NvidiaWizardStep step) => Steps.First(s => s.Step == step);

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

        public void JumpTo(NvidiaWizardStepViewModel step)
        {
            if (step == null || !Current.CanGoBack && Current.Step != NvidiaWizardStep.Welcome) return;
            if (step.IsReachable)
                Current = step;
        }
    }
}
