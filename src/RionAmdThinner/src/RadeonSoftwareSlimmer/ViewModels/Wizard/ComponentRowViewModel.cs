using System;
using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    /// <summary>
    /// One installer item (package / scheduled task / display component) shown on the Customize
    /// page. Wraps the underlying RSS model's keep/enable flag and carries the matched,
    /// evidence-graded recommendation.
    /// </summary>
    public sealed class ComponentRowViewModel : ObservableObject
    {
        private readonly Func<bool> _getKeep;
        private readonly Action<bool> _setKeep;

        public ComponentRowViewModel(string name, string detail, ComponentType type,
            RecommendationRecord rec, Func<bool> getKeep, Action<bool> setKeep)
        {
            Name = name;
            Detail = detail;
            ComponentType = type;
            Recommendation = rec;
            _getKeep = getKeep;
            _setKeep = setKeep;
        }

        public string Name { get; }
        public string Detail { get; }
        public ComponentType ComponentType { get; }
        public RecommendationRecord Recommendation { get; }

        public Classification Classification => Recommendation?.Recommendation ?? Classification.Unknown;

        public bool IsRequired => Classification == Classification.Required;
        public bool IsUnknown => Recommendation == null || Classification == Classification.Unknown;

        public string ClassificationLabel
        {
            get
            {
                switch (Classification)
                {
                    case Classification.Required: return "Required";
                    case Classification.KeepByDefault: return "Keep by default";
                    case Classification.Optional: return "Optional";
                    case Classification.CandidateToDisable: return "Candidate to disable";
                    default: return "Unknown — kept";
                }
            }
        }

        public string EvidenceLabel => Recommendation?.EvidenceGradeLabel ?? "D — Not fully confirmed";
        public string Purpose => Recommendation?.Purpose ?? "This item was not recognised. It is kept by default.";
        public string ExclusionImpact => Recommendation?.FeatureLoss;
        public bool HasSource => !string.IsNullOrWhiteSpace(Recommendation?.SourceTitle);
        public string SourceTitle => Recommendation?.SourceTitle;
        public string SourceUrl => Recommendation?.SourceUrl;

        public bool CanExclude => !IsRequired;

        public bool Keep
        {
            get => _getKeep();
            set
            {
                if (IsRequired) { Raise(nameof(Keep)); return; }   // locked on
                if (_getKeep() == value) return;
                _setKeep(value);
                Raise(nameof(Keep));
                Raise(nameof(WillExclude));
                KeepChanged?.Invoke();
            }
        }

        public bool WillExclude => !Keep;

        public event Action KeepChanged;

        public void Reload() { Raise(nameof(Keep)); Raise(nameof(WillExclude)); }
    }
}
