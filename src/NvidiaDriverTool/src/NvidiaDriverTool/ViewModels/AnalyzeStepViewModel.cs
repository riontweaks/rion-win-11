using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;
using RadeonSoftwareSlimmer.ViewModels;
using NvidiaDriverTool.Services;

namespace NvidiaDriverTool.ViewModels
{
    /// <summary>
    /// Extracts the selected installer (it's a self-extracting 7z archive — same format AMD's
    /// packages use, confirmed against a real 616.56 package on 2026-09-03) and parses its
    /// <c>setup.cfg</c> component manifest. Uses <see cref="SevenZip"/> unchanged — fully
    /// generic, no AMD-specific logic — for the 7z.exe/7z.dll it already unpacks on first use.
    /// </summary>
    public sealed class AnalyzeStepViewModel : NvidiaWizardStepViewModel
    {
        public AnalyzeStepViewModel(NvidiaWizardViewModel wizard)
            : base(wizard, NvidiaWizardStep.Analyze, "Analyze", "")
        {
            RetryCommand = new AsyncRelayCommand(ExtractAsync, () => !IsBusy);
        }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) { NotifyAdvanceChanged(); RetryCommand.RaiseCanExecuteChanged(); } } }

        private string _status = "";
        public string Status { get => _status; private set => Set(ref _status, value); }

        private string _error = "";
        public string Error { get => _error; private set => Set(ref _error, value); }

        private bool _extracted;
        public bool Extracted { get => _extracted; private set { if (Set(ref _extracted, value)) NotifyAdvanceChanged(); } }

        public int ComponentCount => Wizard.Components.Count;
        public string InstallerVersion => Wizard.InstallerInfo?.DriverVersion;
        public bool LooksTrusted => NvidiaInstallPlan.IsTrusted(Wizard.InstallerInfo)
            && Wizard.InstallerInfo?.Publisher?.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0;
        public string Publisher => Wizard.InstallerInfo?.Publisher ?? "Unknown";

        public AsyncRelayCommand RetryCommand { get; }

        public override bool CanAdvance => !IsBusy && Extracted;
        public override bool CanGoBack => !IsBusy;
        public void Reset() { Extracted = false; Error = string.Empty; }

        public override void OnEnter()
        {
            if (!Extracted && !IsBusy) _ = ExtractAsync();
        }

        public bool ContinueToComponents { get; set; }

        private async Task ExtractAsync()
        {
            if (IsBusy) return; if (Wizard.InstallerInfo == null) { Error = "No installer selected."; return; }

            Error = "";
            IsBusy = true;
            Status = "Extracting the installer package...";
            try
            {
                string extractRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NvidiaDriverTool", "extracted", Guid.NewGuid().ToString("N"));

                Directory.CreateDirectory(extractRoot);

                if (!NvidiaInstallPlan.IsTrusted(await Task.Run(() => Wizard.Validator.Inspect(Wizard.InstallerInfo.FilePath))))
                    throw new InvalidOperationException("Select an installer with a valid NVIDIA Corporation signature.");
                using var sevenZip = SevenZip.Acquire();
                var result = await RadeonSoftwareSlimmer.Optimize.ShellRunner.RunAsync(
                    sevenZip.ExecutablePath, $"x \"{Wizard.InstallerInfo.FilePath}\" -o\"{extractRoot}\" -y", null);
                if (result.ExitCode != 0)
                    throw new InvalidOperationException("Extraction failed: " + result.Output);
                Wizard.ExtractedPath = extractRoot;
                Wizard.Components = NvidiaComponentManifest.Parse(extractRoot);
                Status = $"Extracted — found {Wizard.Components.Count} components.";
                Extracted = true;
                Raise(nameof(ComponentCount));
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Status = "";
                Extracted = false;
            }
            finally
            {
                IsBusy = false;
                bool advance = ContinueToComponents; ContinueToComponents = false;
                if (advance && Extracted && Wizard.Current == this) Wizard.NextCommand.Execute(null);
            }
        }
    }
}
