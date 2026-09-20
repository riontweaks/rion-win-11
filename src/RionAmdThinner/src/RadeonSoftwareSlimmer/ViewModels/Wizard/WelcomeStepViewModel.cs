using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public sealed class WelcomeStepViewModel : WizardStepViewModel
    {
        public WelcomeStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.Welcome, "Welcome", "")
        {
            SelectExistingCommand = new RelayCommand(() => Advance(preferDownload: false));
            DownloadCommand = new RelayCommand(() => Advance(preferDownload: true));
            ReleaseCard = new DriverReleaseCardViewModel("AMD", wizard.Hardware.IsAmdGpu ? wizard.Hardware.GpuName : null,
                () => wizard.InstallerInfo, prepareInstaller: async () => {
                    var select = (SelectDriverStepViewModel)wizard.StepFor(WizardStep.SelectDriver);
                    wizard.JumpTo(select);
                    await select.PrepareLatestAsync();
                });
        }

        public HardwareInfo Hardware => Wizard.Hardware;
        public DriverReleaseCardViewModel ReleaseCard { get; }

        public bool ShowOemWarning => Hardware.OemWarning != null;
        public string OemWarning => Hardware.OemWarning;

        public bool GpuIsAmd => Hardware.IsAmdGpu;
        public string NonAmdWarning =>
            GpuIsAmd ? null : "No AMD graphics adapter was detected. This tool only manages AMD driver packages.";

        public RelayCommand SelectExistingCommand { get; }
        public RelayCommand DownloadCommand { get; }

        public override string AdvanceLabel => "Select Driver";

        private void Advance(bool preferDownload)
        {
            var select = (SelectDriverStepViewModel)Wizard.StepFor(WizardStep.SelectDriver);
            select.PreferDownload = preferDownload;
            IsComplete = true;
            if (Wizard.NextCommand.CanExecute(null))
                Wizard.NextCommand.Execute(null);
        }
    }
}
