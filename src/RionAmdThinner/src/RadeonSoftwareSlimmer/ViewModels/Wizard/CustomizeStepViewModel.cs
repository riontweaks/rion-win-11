using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Models.PreInstall;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public enum InstallPreset
    {
        /// <summary>Keep everything - stock AMD install.</summary>
        Default,
        /// <summary>Drop recording / streaming / AMD Link / telemetry / update tasks; keep display, audio, FreeSync/Anti-Lag.</summary>
        OptimalPerformance,
        /// <summary>Display driver + HDMI audio only.</summary>
        Minimal,
        /// <summary>Hand-picked selection.</summary>
        Custom,
    }

    public sealed class CustomizeStepViewModel : WizardStepViewModel
    {
        private readonly RecommendationEngine _rules = new RecommendationEngine();
        private bool _loaded;

        public CustomizeStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.Customize, "Customize Installation", "")
        {
            Packages = new ObservableCollection<ComponentRowViewModel>();
            Tasks = new ObservableCollection<ComponentRowViewModel>();
            Components = new ObservableCollection<ComponentRowViewModel>();

            ApplyPresetCommand = new RelayCommand(p => ApplyPreset((InstallPreset)p));
            RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        }

        private PreInstallViewModel Pre => Wizard.Pre;

        public ObservableCollection<ComponentRowViewModel> Packages { get; }
        public ObservableCollection<ComponentRowViewModel> Tasks { get; }
        public ObservableCollection<ComponentRowViewModel> Components { get; }

        public string RulesVersion => "Recommendation rules " + _rules.RulesVersion;

        private InstallPreset _preset = InstallPreset.Custom;
        public InstallPreset Preset { get => _preset; private set => Set(ref _preset, value); }

        public RelayCommand ApplyPresetCommand { get; }
        public RelayCommand RestoreDefaultsCommand { get; }

        public int ExcludedCount =>
            Packages.Count(r => r.WillExclude) + Tasks.Count(r => r.WillExclude) + Components.Count(r => r.WillExclude);

        public bool ShowAdvancedUnknown { get; set; }

        public override bool CanAdvance => _loaded;
        public override string AdvanceLabel => "Review and Prepare";

        public void Reset() { _loaded = false; Packages.Clear(); Tasks.Clear(); Components.Clear(); }

        public override void OnEnter()
        {
            if (_loaded) { RefreshAll(); return; }

            foreach (PackageModel p in Pre.PackageList.InstallerPackages ?? Enumerable.Empty<PackageModel>())
            {
                var pkg = p;
                RecommendationRecord rec = _rules.Match(pkg.ProductName) ?? _rules.Match(pkg.Description);
                Packages.Add(new ComponentRowViewModel(
                    pkg.ProductName, pkg.Type, ComponentType.Package, rec,
                    () => pkg.Keep, v => pkg.Keep = v) { });
            }

            foreach (ScheduledTaskXmlModel t in Pre.ScheduledTaskList.ScheduledTasks ?? Enumerable.Empty<ScheduledTaskXmlModel>())
            {
                var task = t;
                RecommendationRecord rec = _rules.Match(task.Uri) ?? _rules.Match(task.Command);
                Tasks.Add(new ComponentRowViewModel(
                    task.Uri, task.Command, ComponentType.ScheduledTask, rec,
                    () => task.Enabled, v => task.Enabled = v));
            }

            foreach (DisplayComponentModel c in Pre.DisplayComponentList.DisplayDriverComponents ?? Enumerable.Empty<DisplayComponentModel>())
            {
                var comp = c;
                // every WT6A_INF display component is core — always required
                RecommendationRecord rec = _rules.Match("Display Driver");
                Components.Add(new ComponentRowViewModel(
                    comp.Description ?? comp.InfFile, comp.Directory, ComponentType.DisplayDriverComponent, rec,
                    () => comp.Keep, v => comp.Keep = v));
            }

            foreach (var row in Packages.Concat(Tasks).Concat(Components))
                row.KeepChanged += OnRowKeepChanged;

            _loaded = true;
            NotifyAdvanceChanged();
            RefreshAll();
        }

        private void OnRowKeepChanged()
        {
            Preset = InstallPreset.Custom;
            Raise(nameof(ExcludedCount));
            SyncSession();
        }

        private void ApplyPreset(InstallPreset preset)
        {
            foreach (var row in Packages.Concat(Tasks))
            {
                if (row.IsRequired) { row.Keep = true; continue; }
                switch (preset)
                {
                    case InstallPreset.Default:
                        row.Keep = true;
                        break;
                    case InstallPreset.OptimalPerformance:
                        // drop recording/streaming, AMD Link, telemetry and update tasks; keep the rest
                        row.Keep = !IsPerformanceDrop(row);
                        break;
                    case InstallPreset.Minimal:
                        // display driver + HDMI audio only
                        row.Keep = row.Classification == Classification.Required || IsHdmiAudio(row);
                        break;
                }
            }
            // display components are always kept
            foreach (var c in Components) c.Keep = true;

            Preset = preset;
            Raise(nameof(ExcludedCount));
            SyncSession();
        }

        private static bool IsPerformanceDrop(ComponentRowViewModel row) =>
            row.Recommendation != null &&
            (row.Recommendation.Id == "uxp" || row.Recommendation.Id == "amd-link"
             || row.Recommendation.Id == "install-manager-task" || row.Recommendation.Id == "relive-recording");

        private static bool IsHdmiAudio(ComponentRowViewModel row) =>
            row.Recommendation != null && row.Recommendation.Id == "hd-audio";

        private void RestoreDefaults()
        {
            Pre.ResetInstallerToDefaults();
            foreach (var row in Packages.Concat(Tasks).Concat(Components)) row.Reload();
            Preset = InstallPreset.Default;
            Raise(nameof(ExcludedCount));
            SyncSession();
        }

        private void RefreshAll()
        {
            foreach (var row in Packages.Concat(Tasks).Concat(Components)) row.Reload();
            Raise(nameof(ExcludedCount));
        }

        private void SyncSession()
        {
            Wizard.Session.ExcludedComponentIds = Packages.Concat(Tasks).Concat(Components)
                .Where(r => r.WillExclude).Select(r => r.Name).ToList();
            Wizard.Session.KeptComponentIds = Packages.Concat(Tasks).Concat(Components)
                .Where(r => !r.WillExclude).Select(r => r.Name).ToList();
        }
    }
}
