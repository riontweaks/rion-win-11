using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public sealed class AnalyzeStepViewModel : WizardStepViewModel
    {
        private bool _analyzed;
        private bool _busy;

        public AnalyzeStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.Analyze, "Analyze Installer", "")
        {
            AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !_busy);
        }

        private PreInstallViewModel Pre => Wizard.Pre;
        public DriverInstallerInfo Info => Wizard.InstallerInfo;

        public string WorkspacePath { get; private set; }

        private string _status = "Ready to analyze.";
        public string Status { get => _status; private set => Set(ref _status, value); }

        public bool IsBusy { get => _busy; private set { Set(ref _busy, value); AnalyzeCommand.RaiseCanExecuteChanged(); NotifyAdvanceChanged(); } }

        public int PackageCount { get; private set; }
        public int TaskCount { get; private set; }
        public int ComponentCount { get; private set; }
        public int UnknownCount { get; private set; }
        public bool Analyzed { get => _analyzed; private set { Set(ref _analyzed, value); NotifyAdvanceChanged(); Raise(nameof(SummaryVisible)); } }
        public bool SummaryVisible => _analyzed;

        public AsyncRelayCommand AnalyzeCommand { get; }
        public override bool CanGoBack => !IsBusy;
        public System.Threading.Tasks.Task AnalyzePreparedAsync() => AnalyzeAsync();

        public override bool CanAdvance => _analyzed && !_busy;
        public override string AdvanceLabel => "Customize Installation";

        public override void OnEnter()
        {
            WorkspacePath = Path.Combine(AuditLog.Root, "workspace", Wizard.Session.SessionId);
            Raise(nameof(WorkspacePath));
        }

        private async Task AnalyzeAsync()
        {
            if (IsBusy) return;
            Analyzed = false;
            IsBusy = true;
            Status = "Preparing an isolated workspace…";
            try
            {
                Directory.CreateDirectory(WorkspacePath);
                if (Directory.EnumerateFileSystemEntries(WorkspacePath).Any())
                    Directory.Delete(WorkspacePath, true);
                Directory.CreateDirectory(WorkspacePath);

                Pre.InstallerFiles.ExtractedInstallerDirectory = WorkspacePath;
                Wizard.Session.WorkspacePath = WorkspacePath;

                AuditLog.Action("Analyze", "Extract", Info?.FilePath,
                    newValue: WorkspacePath, reversible: false);

                Status = "Extracting the installer (read-only — the original file is not modified)…";
                await Task.Run(() => Pre.InstallerFiles.ExtractInstallerFiles());

                Status = "Reading installer manifests and building the component inventory…";
                await Task.Run(() => Pre.ReadFromExtractedInstaller());

                PackageCount = Pre.PackageList.InstallerPackages?.Count() ?? 0;
                TaskCount = Pre.ScheduledTaskList.ScheduledTasks?.Count() ?? 0;
                ComponentCount = Pre.DisplayComponentList.DisplayDriverComponents?.Count() ?? 0;
                UnknownCount = 0;   // manifest schema is recognised; unknown files tracked later

                Raise(nameof(PackageCount));
                Raise(nameof(TaskCount));
                Raise(nameof(ComponentCount));
                Raise(nameof(UnknownCount));

                Wizard.Session.Status = SessionStatus.Analyzed;
                ((CustomizeStepViewModel)Wizard.StepFor(WizardStep.Customize)).Reset();
                ((ReviewStepViewModel)Wizard.StepFor(WizardStep.Review)).ResetPreparation();
                Analyzed = true;
                Status = $"Analysis complete — {PackageCount} packages, {TaskCount} scheduled tasks, {ComponentCount} display components.";
            }
            catch (Exception ex)
            {
                Analyzed = false;
                Status = "Analysis failed: " + ex.Message + "  (the original installer was not changed; see the Activity Log).";
                AuditLog.Action("Analyze", "Extract", Info?.FilePath, result: "Error", error: ex.Message, reversible: false);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
