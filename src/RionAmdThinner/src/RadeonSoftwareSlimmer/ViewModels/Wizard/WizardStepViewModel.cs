using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public abstract class WizardStepViewModel : ObservableObject
    {
        protected WizardStepViewModel(WizardViewModel wizard, WizardStep step, string title, string glyph)
        {
            Wizard = wizard;
            Step = step;
            Title = title;
            Glyph = glyph;
        }

        protected WizardViewModel Wizard { get; }

        public WizardStep Step { get; }
        public string Title { get; }
        public string Glyph { get; }
        public int Number => (int)Step + 1;

        private bool _isReachable;
        public bool IsReachable
        {
            get => _isReachable;
            set => Set(ref _isReachable, value);
        }

        private bool _isCurrent;
        public bool IsCurrent
        {
            get => _isCurrent;
            set { if (Set(ref _isCurrent, value)) Raise(nameof(RailState)); }
        }

        /// <summary>"active" when this step is the one showing (and no rail tool is open); drives the rail highlight.</summary>
        public string RailState => _isCurrent && !Wizard.IsToolActive ? "active" : null;

        /// <summary>Lets the wizard re-raise <see cref="RailState"/> when a rail tool opens/closes.</summary>
        public void RaiseRailState() => Raise(nameof(RailState));

        private bool _isComplete;
        public bool IsComplete
        {
            get => _isComplete;
            set => Set(ref _isComplete, value);
        }

        /// <summary>Gate for the Next button. Override where a step must be finished first.</summary>
        public virtual bool CanAdvance => true;

        /// <summary>Label for the primary "advance" button on this step.</summary>
        public virtual string AdvanceLabel => "Next";

        /// <summary>True to show a Back button.</summary>
        public virtual bool CanGoBack => Step != WizardStep.Welcome;

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
