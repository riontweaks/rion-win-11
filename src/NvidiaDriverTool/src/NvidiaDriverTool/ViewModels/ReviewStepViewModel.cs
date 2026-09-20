using System.Linq;

namespace NvidiaDriverTool.ViewModels
{
    public sealed class ReviewStepViewModel : NvidiaWizardStepViewModel
    {
        public ReviewStepViewModel(NvidiaWizardViewModel wizard)
            : base(wizard, NvidiaWizardStep.Review, "Review", "")
        {
            PrepareCommand = new RadeonSoftwareSlimmer.ViewModels.AsyncRelayCommand(PrepareAsync, () => !Preparing && string.IsNullOrEmpty(Error) && Wizard.Components.Count > 0);
            ExportInstallerCommand = new RadeonSoftwareSlimmer.ViewModels.AsyncRelayCommand(ExportInstallerAsync, () => CanAdvance);
            CancelExportCommand = new RadeonSoftwareSlimmer.ViewModels.RelayCommand(() => _exportCancellation?.Cancel());
        }

        public RadeonSoftwareSlimmer.ViewModels.AsyncRelayCommand PrepareCommand { get; }
        public bool Preparing { get; private set; }
        public bool CanEditOptions => !Preparing;
        public bool Unattended
        {
            get => Wizard.InstallationOptions.Unattended;
            set { if (Preparing) return; Wizard.InstallationOptions = Wizard.InstallationOptions with { Unattended = value }; OptionsChanged(); }
        }
        public bool CleanInstall
        {
            get => Wizard.InstallationOptions.CleanInstall;
            set { if (Preparing) return; Wizard.InstallationOptions = Wizard.InstallationOptions with { CleanInstall = value }; OptionsChanged(); }
        }
        public string InstallationSummary => Wizard.InstallationOptions.Description;
        private void OptionsChanged()
        {
            Raise(nameof(Unattended)); Raise(nameof(CleanInstall)); Raise(nameof(InstallationSummary));
            PreparationStatus = "Installation choices changed. Prepare or export again to save them.";
            Raise(nameof(PreparationStatus));
        }
        public RadeonSoftwareSlimmer.ViewModels.AsyncRelayCommand ExportInstallerCommand { get; }
        public RadeonSoftwareSlimmer.ViewModels.RelayCommand CancelExportCommand { get; }
        private System.Threading.CancellationTokenSource _exportCancellation;
        public bool IsExporting => _exportCancellation != null;
        private async System.Threading.Tasks.Task ExportInstallerAsync()
        {
            if (!CanAdvance) return;
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where to export the NVIDIA installer" };
            if (dialog.ShowDialog() != true) return;
            var root = Wizard.ExtractedPath;
            var exclusions = Wizard.ExcludedComponents.ToArray();
            var options = Wizard.InstallationOptions;
            Preparing = true; Raise(nameof(CanEditOptions)); _exportCancellation = new System.Threading.CancellationTokenSource();
            Raise(nameof(IsExporting)); PrepareCommand.RaiseCanExecuteChanged(); ExportInstallerCommand.RaiseCanExecuteChanged(); NotifyAdvanceChanged();
            try
            {
                var path = await Services.NvidiaPackagePreparation.ExportAsync(root, dialog.FolderName, exclusions,
                    new System.Progress<string>(text => { PreparationStatus = text; Raise(nameof(PreparationStatus)); }), _exportCancellation.Token, options);
                PreparationStatus = "Installer exported to " + path + ". Use Install-selected-packages.cmd to apply the saved choices. Nothing was installed.";
            }
            catch (System.Exception ex) { PreparationStatus = ex.Message; }
            finally
            {
                _exportCancellation.Dispose(); _exportCancellation = null; Preparing = false; Raise(nameof(CanEditOptions));
                Raise(nameof(IsExporting)); Raise(nameof(PreparationStatus)); PrepareCommand.RaiseCanExecuteChanged(); ExportInstallerCommand.RaiseCanExecuteChanged(); NotifyAdvanceChanged();
            }
        }
        public string PreparationStatus { get; private set; } = "Prepare validates the selected packages and saves a plan without launching setup.";
        private async System.Threading.Tasks.Task PrepareAsync()
        {
            if (Preparing) return;
            Preparing = true; Raise(nameof(CanEditOptions)); PrepareCommand.RaiseCanExecuteChanged(); NotifyAdvanceChanged();
            ExportInstallerCommand.RaiseCanExecuteChanged();
            PreparationStatus = "Checking NVIDIA signature, package dependencies and manifests…"; Raise(nameof(PreparationStatus));
            try
            {
                var result = await Services.NvidiaPackagePreparation.PrepareAsync(Wizard.ExtractedPath, Wizard.ExcludedComponents, Wizard.InstallationOptions);
                PreparationStatus = $"Prepared: {result.Kept} kept, {result.Excluded} excluded. No installer launched.\nPlan: {result.PlanPath}";
            }
            catch (System.Exception ex) { PreparationStatus = "Preparation failed: " + ex.Message; }
            finally { Preparing = false; Raise(nameof(CanEditOptions)); Raise(nameof(PreparationStatus)); PrepareCommand.RaiseCanExecuteChanged(); ExportInstallerCommand.RaiseCanExecuteChanged(); NotifyAdvanceChanged(); }
        }
        public override bool CanGoBack => !Preparing;

        public int TotalCount => Wizard.Components.Count;
        public int ExcludedCount => Wizard.ExcludedComponents.Count;
        public int InstallCount => TotalCount - ExcludedCount;

        public string ExcludedSummary => ExcludedCount == 0
            ? "Every component will be installed."
            : string.Join(", ", Wizard.Components
                .Where(c => Wizard.ExcludedComponents.Contains(c.Name))
                .Select(c => Services.NvidiaComponentManifest.DisplayName(c)));

        public override void OnEnter()
        {
            try { Services.NvidiaInstallPlan.Arguments(Wizard.Components, Wizard.ExcludedComponents, Wizard.InstallationOptions); Error = ""; }
            catch (System.Exception ex) { Error = ex.Message; }
            Raise(nameof(Error));
            Raise(nameof(Unattended)); Raise(nameof(CleanInstall)); Raise(nameof(InstallationSummary));
            NotifyAdvanceChanged();
            Raise(nameof(TotalCount));
            Raise(nameof(ExcludedCount));
            Raise(nameof(InstallCount));
            Raise(nameof(ExcludedSummary));
            PreparationStatus = "Prepare validates the selected packages and saves a plan without launching setup.";
            Raise(nameof(PreparationStatus)); PrepareCommand.RaiseCanExecuteChanged();
            ExportInstallerCommand.RaiseCanExecuteChanged();
        }

        public override string AdvanceLabel => "Install";
        public string Error { get; private set; } = "";
        public override bool CanAdvance => !Preparing && string.IsNullOrEmpty(Error) && Wizard.Components.Count > 0;
    }
}
