using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public sealed class FinishStepViewModel : WizardStepViewModel
    {
        public FinishStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.Finish, "Finish and Export", "")
        {
            ExportReportCommand = new RelayCommand(ExportReport);
            OpenAmdSoftwareCommand = new RelayCommand(OpenAmdSoftware);
            OpenSessionFolderCommand = new RelayCommand(OpenSessionFolder);
        }

        private CustomizeStepViewModel Customize =>
            (CustomizeStepViewModel)Wizard.StepFor(WizardStep.Customize);

        public HardwareInfo Hardware => Wizard.Hardware;
        public DriverInstallerInfo Info => Wizard.InstallerInfo;

        public string DriverInstalled =>
            "AMD Software: Adrenalin Edition " + (Info?.PackageVersion ?? "");

        public System.Collections.Generic.IEnumerable<string> Excluded =>
            Customize.Packages.Concat(Customize.Tasks).Where(r => r.WillExclude).Select(r => r.Name);

        public bool AnythingExcluded => Excluded.Any();

        private string _status;
        public string Status { get => _status; private set => Set(ref _status, value); }

        public string BackupNote =>
            "A session backup and restoration manifest were created at " + AuditLog.SessionDir(Wizard.Session.SessionId);

        public RelayCommand ExportReportCommand { get; }
        public RelayCommand OpenAmdSoftwareCommand { get; }
        public RelayCommand OpenSessionFolderCommand { get; }

        public override bool CanAdvance => false;
        public override string AdvanceLabel => "Done";

        public override void OnEnter()
        {
            Wizard.Session.Status = SessionStatus.Complete;
            AuditLog.Action("Finish", "SessionComplete", Wizard.Session.SessionId, reversible: false);
        }

        private void ExportReport()
        {
            try
            {
                string dir = AuditLog.SessionDir(Wizard.Session.SessionId);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "session-report.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(new
                {
                    Wizard.Session,
                    Hardware,
                    Installer = Info,
                    Excluded = Excluded.ToList(),
                    GeneratedUtc = DateTime.UtcNow,
                }, Formatting.Indented));
                Status = "Report written to " + path;
            }
            catch (Exception ex) { Status = "Export failed: " + ex.Message; }
        }

        private void OpenAmdSoftware()
        {
            try { Process.Start(new ProcessStartInfo("radeonsoftware:") { UseShellExecute = true }); }
            catch
            {
                try { Process.Start(new ProcessStartInfo("RadeonSoftware.exe") { UseShellExecute = true }); }
                catch (Exception ex) { Status = "Could not open AMD Software: " + ex.Message; }
            }
        }

        private void OpenSessionFolder()
        {
            try { Process.Start(new ProcessStartInfo(AuditLog.SessionDir(Wizard.Session.SessionId)) { UseShellExecute = true }); }
            catch (Exception ex) { Status = "Could not open the folder: " + ex.Message; }
        }
    }
}
