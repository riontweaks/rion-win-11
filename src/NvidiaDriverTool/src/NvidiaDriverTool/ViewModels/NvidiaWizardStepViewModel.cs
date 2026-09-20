using RadeonSoftwareSlimmer.ViewModels;

namespace NvidiaDriverTool.ViewModels
{
    /// <summary>Mirrors <c>RadeonSoftwareSlimmer.ViewModels.Wizard.WizardStepViewModel</c>
    /// exactly — that base class has no AMD-specific logic at all, so the shape is copied rather
    /// than referenced only because it's a different concrete step set (see NvidiaWizardStep).</summary>
    public abstract class NvidiaWizardStepViewModel : ObservableObject
    {
        protected NvidiaWizardStepViewModel(NvidiaWizardViewModel wizard, NvidiaWizardStep step, string title, string glyph)
        {
            Wizard = wizard;
            Step = step;
            Title = title;
            Glyph = glyph;
        }

        protected NvidiaWizardViewModel Wizard { get; }

        public NvidiaWizardStep Step { get; }
        public string Title { get; }
        public string Glyph { get; }
        public int Number => Wizard.Steps.IndexOf(this) + 1;

        private bool _isReachable;
        public bool IsReachable { get => _isReachable; set => Set(ref _isReachable, value); }

        private bool _isCurrent;
        public bool IsCurrent { get => _isCurrent; set { if (Set(ref _isCurrent, value)) Raise(nameof(RailState)); } }

        public string RailState => _isCurrent ? "active" : null;

        private bool _isComplete;
        public bool IsComplete { get => _isComplete; set => Set(ref _isComplete, value); }

        /// <summary>Gate for the Next button. Override where a step must be finished first.</summary>
        public virtual bool CanAdvance => true;

        public virtual string AdvanceLabel => "Next";

        public virtual bool CanGoBack => Step != NvidiaWizardStep.Welcome;

        public virtual void OnEnter() { }
        public virtual void OnLeave() { }

        protected void NotifyAdvanceChanged()
        {
            Raise(nameof(CanAdvance));
            Raise(nameof(CanGoBack));
            Wizard.RefreshCommands();
        }
    }
}
