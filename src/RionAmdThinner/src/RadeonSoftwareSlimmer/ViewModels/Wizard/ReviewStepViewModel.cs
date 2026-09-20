using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public sealed class ReviewStepViewModel : WizardStepViewModel
    {
        private bool _prepared;
        private bool _busy;
        private string _preparedSelection = "";
        private System.Threading.CancellationTokenSource _exportCancellation;

        public ReviewStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.Review, "Review and Prepare", "")
        {
            PrepareCommand = new AsyncRelayCommand(PrepareAsync, () => !_busy && !_prepared);
            ExportPlanCommand = new RelayCommand(ExportPlan);
            ExportInstallerCommand = new AsyncRelayCommand(ExportInstallerAsync, () => !IsBusy && Prepared);
            CancelExportCommand = new RelayCommand(() => _exportCancellation?.Cancel());
        }

        private CustomizeStepViewModel Customize =>
            (CustomizeStepViewModel)Wizard.StepFor(WizardStep.Customize);

        public HardwareInfo Hardware => Wizard.Hardware;
        public DriverInstallerInfo Info => Wizard.InstallerInfo;
        public string WorkspacePath => Wizard.Session.WorkspacePath;

        public System.Collections.Generic.IEnumerable<string> Keeping =>
            Customize.Packages.Concat(Customize.Tasks).Concat(Customize.Components).Where(r => !r.WillExclude).Select(r => r.Name);
        public System.Collections.Generic.IEnumerable<string> Excluding =>
            Customize.Packages.Concat(Customize.Tasks).Concat(Customize.Components).Where(r => r.WillExclude).Select(r => r.Name);

        public bool AnythingExcluded => Excluding.Any();

        private string _status = "Review the plan below, then Prepare Installer.";
        public string Status { get => _status; private set => Set(ref _status, value); }

        public bool Prepared { get => _prepared; private set { Set(ref _prepared, value); NotifyAdvanceChanged(); ExportInstallerCommand.RaiseCanExecuteChanged(); } }
        public bool IsBusy { get => _busy; private set { Set(ref _busy, value); PrepareCommand.RaiseCanExecuteChanged(); ExportInstallerCommand.RaiseCanExecuteChanged(); NotifyAdvanceChanged(); } }
        private bool _exporting;
        public bool IsExporting { get => _exporting; private set => Set(ref _exporting, value); }
        public AsyncRelayCommand ExportInstallerCommand { get; }
        public RelayCommand CancelExportCommand { get; }
        private string SelectionKey => string.Join("\n", Keeping.OrderBy(x => x).Select(x => "+" + x).Concat(Excluding.OrderBy(x => x).Select(x => "-" + x)));
        public override void OnEnter()
        {
            if (Prepared && _preparedSelection != SelectionKey) ResetPreparation();
            Raise(nameof(Keeping)); Raise(nameof(Excluding)); Raise(nameof(AnythingExcluded));
        }

        private async Task ExportInstallerAsync()
        {
            if (IsBusy || !Prepared) return;
            if (_preparedSelection != SelectionKey) { ResetPreparation(); return; }
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where to export the AMD installer" };
            if (dialog.ShowDialog() != true) return;
            string source = Wizard.Pre.InstallerFiles.ExtractedInstallerDirectory;
            var kept = Keeping.ToArray(); var excluded = Excluding.ToArray();
            IsBusy = true; IsExporting = true;
            _exportCancellation = new System.Threading.CancellationTokenSource();
            try
            {
                string path = await InstallerFolderExport.ExportAsync(source, dialog.FolderName, "AMD", async folder =>
                {
                    await File.WriteAllTextAsync(Path.Combine(folder, "Rion-export-selections.json"), JsonConvert.SerializeObject(new { Vendor = "AMD", Keeping = kept, Excluding = excluded }, Formatting.Indented));
                    await File.WriteAllTextAsync(Path.Combine(folder, "README-Rion.txt"), "AMD customized installer\r\n\r\nRun Setup.exe from this folder. Keep the entire folder together.\r\nPackage, display-component and scheduled-task edits are included.\r\nRion post-install GPU tweaks and live service changes are not included.\r\nUse only on hardware and Windows versions supported by this driver.\r\nNo installation was performed during export.\r\n");
                }, new Progress<string>(text => Status = text), _exportCancellation.Token);
                Status = "Installer exported to " + path + ". Run Setup.exe there when ready; nothing was installed.";
            }
            catch (Exception ex) { Status = ex.Message; }
            finally { _exportCancellation.Dispose(); _exportCancellation = null; IsExporting = false; IsBusy = false; }
        }

        public string BackupManifestPath => Wizard.Session.BackupManifestPath;

        public AsyncRelayCommand PrepareCommand { get; }
        public RelayCommand ExportPlanCommand { get; }
        public void ResetPreparation()
        {
            Prepared = false;
            Status = "Review the new package, then Prepare Installer.";
            PrepareCommand.RaiseCanExecuteChanged();
        }

        public override bool CanAdvance => _prepared && !IsBusy;
        public override bool CanGoBack => !IsBusy;
        public override string AdvanceLabel => "Choose Services & Tweaks";

        private async Task PrepareAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                WriteBackupManifest();

                Status = "Applying package changes to the workspace copy only…";
                await Task.Run(() => Wizard.Pre.ModifyInstaller());
                if (!string.IsNullOrEmpty(Wizard.Pre.LastModificationError)) throw new InvalidOperationException(Wizard.Pre.LastModificationError);

                if (!Wizard.Pre.InstallerFiles.ValidateExtractedLocation())
                {
                    Status = "Preparation validation failed — the workspace no longer looks like a complete installer. Nothing was installed.";
                    return;
                }

                Wizard.Session.Status = SessionStatus.Prepared;
                Wizard.Session.PackageChanges = Excluding.Select(x => "Excluded: " + x).ToList();
                AuditLog.Action("Prepare installer", "PrepareWorkingCopy", WorkspacePath,
                    newValue: $"{Excluding.Count()} excluded", reversible: false);

                _preparedSelection = SelectionKey;
                Prepared = true;
                Status = "Modified working copy is ready. The original installer was not touched.";
            }
            catch (Exception ex)
            {
                Status = "Preparation failed: " + ex.Message;
                AuditLog.Action("Prepare installer", "PrepareWorkingCopy", WorkspacePath, result: "Error", error: ex.Message, reversible: false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void WriteBackupManifest()
        {
            try
            {
                string dir = AuditLog.SessionDir(Wizard.Session.SessionId);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "backup-manifest.json");
                var manifest = new
                {
                    Wizard.Session.SessionId,
                    CreatedUtc = DateTime.UtcNow,
                    OriginalInstaller = Info?.FilePath,
                    OriginalHashSha256 = Info?.OriginalHashSha256,
                    Workspace = WorkspacePath,
                    Excluding = Excluding.ToList(),
                    Keeping = Keeping.ToList(),
                    Note = "To restore an excluded package, re-run the preserved original installer with that package included.",
                };
                File.WriteAllText(path, JsonConvert.SerializeObject(manifest, Formatting.Indented));
                Wizard.Session.BackupManifestPath = path;
                Raise(nameof(BackupManifestPath));
            }
            catch (Exception ex)
            {
                Status = "Could not write the backup manifest: " + ex.Message;
            }
        }

        private void ExportPlan()
        {
            try
            {
                string dir = AuditLog.SessionDir(Wizard.Session.SessionId);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "session-plan.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(Wizard.Session, Formatting.Indented));
                Status = "Plan exported to " + path;
            }
            catch (Exception ex)
            {
                Status = "Export failed: " + ex.Message;
            }
        }
    }
}
