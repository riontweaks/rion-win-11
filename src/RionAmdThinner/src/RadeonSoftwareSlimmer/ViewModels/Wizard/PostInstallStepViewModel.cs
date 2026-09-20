using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Adlx;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Models.PostInstall;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    /// <summary>
    /// "Choose Services &amp; Tweaks" - shown before the install runs. The user picks which AMD
    /// services / scheduled tasks to disable (via the hosted RadeonSoftwareSlimmer panel). The
    /// choices are applied automatically by the Installation Monitor once the install verifies.
    /// </summary>
    public sealed class PostInstallStepViewModel : WizardStepViewModel
    {
        private readonly RecommendationEngine _rules = new RecommendationEngine();

        public PostInstallStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.Configure, "Choose Services & Tweaks", "")
        {
            RefreshCommand = new AsyncRelayCommand(() => Post.LoadOrRefreshAsync(true));
            SelectRecommendedCommand = new RelayCommand(SelectRecommended);
        }

        /// <summary>RadeonSoftwareSlimmer post-install engine - real installed services / tasks / host processes / temp files.</summary>
        public PostInstallViewModel Post => Wizard.Post;

        public HardwareInfo Hardware => Wizard.Hardware;
        public IAdlxService Adlx => Wizard.Adlx;
        public AdlxCapabilitySet AdlxCaps => Wizard.Adlx.DetectCapabilities();
        public string AdlxUnavailable => Wizard.Adlx.UnavailableReason;
        public bool AdlxAvailable => AdlxCaps.AnythingAvailable;

        public AsyncRelayCommand RefreshCommand { get; }
        public RelayCommand SelectRecommendedCommand { get; }

        public override string AdvanceLabel => "Install and Verify";
        public override bool CanAdvance => true;

        public override async void OnEnter()
        {
            // Refresh every time the step is opened so it reflects the current state of the
            // services / tasks (the user may have changed them since last visit, or from the
            // Post-Install tool). The hosted panel rebinds to the refreshed lists.
            await Post.LoadOrRefreshAsync(false);
        }

        public override void OnLeave()
        {
            BuildPlan(Wizard.Monitor);
        }

        /// <summary>Ticks off the Optional services / tasks the evidence-graded rules recommend disabling. Selection only.</summary>
        private void SelectRecommended()
        {
            foreach (ServiceModel service in Post.ServiceList.Services ?? Enumerable.Empty<ServiceModel>())
            {
                if (service.Enabled && ShouldDisable(service.Name, service.DisplayName))
                    service.Enabled = false;
            }

            foreach (ScheduledTaskModel task in Post.RadeonScheduledTaskList.RadeonScheduledTasks ?? Enumerable.Empty<ScheduledTaskModel>())
            {
                if (task.Enabled && ShouldDisable(task.Name, task.Description))
                    task.Enabled = false;
            }

            StaticViewModel.AddLogMessage("Selected the recommended AMD service / task changes. Nothing is applied until after the install verifies.");
        }

        /// <summary>Fills the monitor's "will be applied" list from the current selection (rows the user unticked).</summary>
        public void BuildPlan(InstallMonitor monitor)
        {
            monitor.PlannedServiceChanges.Clear();

            foreach (ServiceModel service in Post.ServiceList.Services ?? Enumerable.Empty<ServiceModel>())
            {
                if (!service.Enabled)
                    monitor.PlannedServiceChanges.Add("Disable service: " + service.Name);
            }

            foreach (ScheduledTaskModel task in Post.RadeonScheduledTaskList.RadeonScheduledTasks ?? Enumerable.Empty<ScheduledTaskModel>())
            {
                if (!task.Enabled)
                    monitor.PlannedServiceChanges.Add("Disable task: " + task.Name);
            }
        }

        /// <summary>Applies the current service / task selection. Called by the Installation Monitor after a verified install.</summary>
        public async Task ApplySelectedAsync(InstallMonitor monitor)
        {
            await Post.LoadOrRefreshAsync(false);

            // Re-apply the recommended selection: the fresh install re-enables everything.
            SelectRecommended();
            BuildPlan(monitor);

            List<string> planned = monitor.PlannedServiceChanges.ToList();
            if (planned.Count == 0)
            {
                monitor.AddLog("No service or task changes were selected.");
                AuditLog.Action("Auto-apply service changes", "PostInstallAutoApply", "services+tasks",
                    newValue: "nothing selected", reversible: true);
                return;
            }

            await Post.ApplyChangesAsync();

            foreach (string item in planned)
                monitor.RecordApplied(item);

            AuditLog.Action("Auto-apply service changes", "PostInstallAutoApply", "services+tasks",
                newValue: string.Join("; ", planned), result: "OK", reversible: true);
        }

        private bool ShouldDisable(params string[] identifiers) =>
            identifiers.Any(id => _rules.ShouldAutoDisable(id));
    }
}
