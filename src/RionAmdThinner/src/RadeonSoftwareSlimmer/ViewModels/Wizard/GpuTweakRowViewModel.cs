using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    /// <summary>One selectable GPU tweak on the Install &amp; Verify list.</summary>
    public sealed class GpuTweakRowViewModel : ObservableObject
    {
        private bool _selected;

        public GpuTweakRowViewModel(GpuTweak tweak, bool alreadyApplied)
        {
            Tweak = tweak;
            AlreadyApplied = alreadyApplied;
            _selected = tweak.RecommendedDefault && !alreadyApplied;
        }

        public GpuTweak Tweak { get; }

        public string Title => Tweak.Title;
        public string Summary => Tweak.Summary;
        public string TradeOff => Tweak.TradeOff;
        public string EvidenceLabel => Tweak.EvidenceGradeLabel;
        public string SourceNote => Tweak.SourceNote;

        /// <summary>The tweak's value is already set on this machine.</summary>
        public bool AlreadyApplied { get; }
        public string StateNote => AlreadyApplied ? "Already applied" : null;

        public bool Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Raise(nameof(WillApply)); }
        }

        /// <summary>
        /// Queued to be written after a verified install. Driven purely by the checkbox - an
        /// "Already applied" tweak the user ticks anyway still goes in the queue (the apply step
        /// treats an unchanged value as a no-op, so it costs nothing and keeps the list honest).
        /// <see cref="AlreadyApplied"/> only decides the initial checked state.
        /// </summary>
        public bool WillApply => _selected;
    }
}
