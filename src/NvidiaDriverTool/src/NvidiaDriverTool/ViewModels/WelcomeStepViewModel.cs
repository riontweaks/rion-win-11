using RadeonSoftwareSlimmer.ViewModels;

namespace NvidiaDriverTool.ViewModels
{
    public sealed class WelcomeStepViewModel : NvidiaWizardStepViewModel
    {
        public WelcomeStepViewModel(NvidiaWizardViewModel wizard)
            : base(wizard, NvidiaWizardStep.Welcome, "Welcome", "")
        {
            ContinueCommand = new RelayCommand(() => { IsComplete = true; Wizard.NextCommand.Execute(null); });
            ReleaseCard = new DriverReleaseCardViewModel("NVIDIA", wizard.GpuIsNvidia ? wizard.Hardware.GpuName : null,
                () => wizard.InstallerInfo, prepareInstaller: async () => {
                    var select = (SelectDriverStepViewModel)wizard.StepFor(NvidiaWizardStep.SelectDriver);
                    wizard.JumpTo(select);
                    await select.PrepareLatestAsync();
                });
        }

        public string GpuName => string.IsNullOrEmpty(Wizard.Hardware.GpuName) ? "No GPU detected" : Wizard.Hardware.GpuName;
        public bool GpuIsNvidia => Wizard.GpuIsNvidia;
        public string NonNvidiaWarning => GpuIsNvidia ? null :
            "No NVIDIA graphics adapter was detected. This tool only manages NVIDIA driver packages.";

        public string Disclaimer =>
            "This application is independent software and is not affiliated with, endorsed by, or supported by NVIDIA.";

        public RelayCommand ContinueCommand { get; }
        public DriverReleaseCardViewModel ReleaseCard { get; }

        public override string AdvanceLabel => "Get Started";
    }
}
