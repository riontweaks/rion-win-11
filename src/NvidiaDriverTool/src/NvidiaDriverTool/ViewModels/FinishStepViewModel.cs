namespace NvidiaDriverTool.ViewModels
{
    public sealed class FinishStepViewModel : NvidiaWizardStepViewModel
    {
        public FinishStepViewModel(NvidiaWizardViewModel wizard)
            : base(wizard, NvidiaWizardStep.Finish, "Finish", "")
        {
            OpenPostInstallCommand = new RadeonSoftwareSlimmer.ViewModels.RelayCommand(() => Wizard.OpenPostInstall?.Invoke());
            ModifyPackagesCommand = new RadeonSoftwareSlimmer.ViewModels.RelayCommand(Wizard.ModifyPackage);
        }
        public bool ShowAdvancedTools => RadeonSoftwareSlimmer.Services.EditionPolicy.HasAdvancedTools;
        public RadeonSoftwareSlimmer.ViewModels.RelayCommand OpenPostInstallCommand { get; }
        public RadeonSoftwareSlimmer.ViewModels.RelayCommand ModifyPackagesCommand { get; }

        public string DriverVersion => Wizard.InstallerInfo?.DriverVersion ?? "Unknown";
        public int InstalledCount => Wizard.Components.Count - Wizard.ExcludedComponents.Count;
        public int ExcludedCount => Wizard.ExcludedComponents.Count;

        public override bool CanAdvance => false;
        public override bool CanGoBack => false;
    }
}
