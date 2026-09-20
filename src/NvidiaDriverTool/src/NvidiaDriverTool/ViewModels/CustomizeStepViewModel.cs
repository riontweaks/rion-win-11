using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;
using RadeonSoftwareSlimmer.ViewModels.Wizard;
using NvidiaDriverTool.Services;

namespace NvidiaDriverTool.ViewModels
{
    /// <summary>
    /// The component picker — reuses <c>ComponentRowViewModel</c> from RadeonSoftwareSlimmer
    /// completely unchanged (confirmed: it has zero AMD-specific logic, just name/detail/keep-flag/
    /// evidence-graded-recommendation). Only the mapping from NVIDIA's real
    /// <c>setup.cfg</c> disposition to the shared <c>Classification</c> enum is new — see
    /// <see cref="NvidiaComponentManifest.ToClassification"/>, tested against the real manifest.
    /// </summary>
    public sealed class CustomizeStepViewModel : NvidiaWizardStepViewModel
    {
        public CustomizeStepViewModel(NvidiaWizardViewModel wizard, bool background = false)
            : base(wizard, background ? NvidiaWizardStep.Background : NvidiaWizardStep.Customize,
                background ? "Services & Tasks" : "Choose Components", "")
        {
            _background = background;
            KeepAllCommand = new RelayCommand(() => ApplyPreset(NvidiaPackagePreset.KeepAll));
            WithoutAppCommand = new RelayCommand(() => ApplyPreset(NvidiaPackagePreset.WithoutCompanionApp));
            DriverFocusedCommand = new RelayCommand(() => ApplyPreset(NvidiaPackagePreset.DriverFocused));
        }
        private readonly bool _background;
        public bool ShowPresets => !_background;
        public RelayCommand KeepAllCommand { get; }
        public RelayCommand WithoutAppCommand { get; }
        public RelayCommand DriverFocusedCommand { get; }
        public int TotalCount => Wizard.Components.Count;
        public string PresetStatus { get; private set; } = "Presets keep required packages, their dependencies and unknown packages. Review every exclusion before installing.";
        private string _search = "";
        public string Search
        {
            get => _search;
            set { _search = value ?? ""; Raise(nameof(Search)); System.Windows.Data.CollectionViewSource.GetDefaultView(Rows).Refresh(); }
        }
        private void ApplyPreset(NvidiaPackagePreset preset)
        {
            try
            {
                var excluded = NvidiaPackagePresets.Exclusions(Wizard.Components, preset);
                Wizard.ExcludedComponents.Clear(); Wizard.ExcludedComponents.UnionWith(excluded);
                OnEnter();
                PresetStatus = $"Preset applied: {excluded.Count} packages excluded. Dependencies and unknown packages retained. Review the list before continuing.";
            }
            catch (System.Exception ex) { PresetStatus = ex.Message; }
            Raise(nameof(PresetStatus));
        }
        public string ScopeNote => _background
            ? "These service and scheduled-task definitions come from this package. Toggles exclude their whole owning package, including its features. Dependencies are checked at Review."
            : "Choose NVIDIA packages to keep. Exclusions affect this installation; they do not uninstall existing components. NVIDIA may apply additional hardware conditions.";

        public ObservableCollection<ComponentRowViewModel> Rows { get; } = new ObservableCollection<ComponentRowViewModel>();

        public override void OnEnter()
        {
            Rows.Clear();
            System.Windows.Data.CollectionViewSource.GetDefaultView(Rows).Filter = item => item is ComponentRowViewModel row &&
                (string.IsNullOrWhiteSpace(Search) || (row.Name + " " + row.Detail + " " + row.Purpose).IndexOf(Search, System.StringComparison.OrdinalIgnoreCase) >= 0);
            foreach (NvidiaComponentManifest.NvidiaComponent c in Wizard.Components)
            {
                if (_background && c.Services.Count == 0 && c.Tasks.Count == 0) continue;
                Classification classification = NvidiaComponentManifest.ToClassification(c);
                var rec = new RecommendationRecord
                {
                    Id = c.Name,
                    Recommendation = classification,
                    EvidenceGrade = EvidenceGrade.D_Unverified,
                    Purpose = classification == Classification.Required
                        ? "Required for the driver to install and function."
                        : "Not yet individually researched — kept by default; exclude if you know you don't need it.",
                };

                string name = c.Name;
                var row = new ComponentRowViewModel(
                    NvidiaComponentManifest.DisplayName(c),
                    _background ? c.Name + " · Services: " + string.Join(", ", c.Services) + " · Tasks: " + string.Join(", ", c.Tasks) : c.Name,
                    ComponentType.Package,
                    rec,
                    getKeep: () => !Wizard.ExcludedComponents.Contains(name),
                    setKeep: keep =>
                    {
                        if (keep) Wizard.ExcludedComponents.Remove(name);
                        else Wizard.ExcludedComponents.Add(name);
                    });
                row.KeepChanged += RecountExclusions;
                Rows.Add(row);
            }
            Raise(nameof(ExcludedCount)); Raise(nameof(TotalCount));
        }

        public int ExcludedCount => Wizard.ExcludedComponents.Count;

        public void RecountExclusions() => Raise(nameof(ExcludedCount));
    }
}
