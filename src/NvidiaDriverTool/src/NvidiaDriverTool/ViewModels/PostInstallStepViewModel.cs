namespace NvidiaDriverTool.ViewModels
{
    public sealed class PostInstallStepViewModel : NvidiaWizardStepViewModel
    {
        public PostInstallStepViewModel(NvidiaWizardViewModel wizard)
            : base(wizard, NvidiaWizardStep.PostInstall, "Post-Install", "") { }
        public override string AdvanceLabel => "Finish";
        private object _content;
        public object Content => _content ??= Wizard.PostInstallContentFactory?.Invoke();
        public override void OnEnter() { Raise(nameof(Content)); }
    }
}
