using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.ViewModels;

namespace NvidiaDriverTool.ViewModels
{
    public sealed class SelectDriverStepViewModel : NvidiaWizardStepViewModel
    {
        public SelectDriverStepViewModel(NvidiaWizardViewModel wizard)
            : base(wizard, NvidiaWizardStep.SelectDriver, "Select Driver", "")
        {
            DownloadCommand = new AsyncRelayCommand(DownloadLatestAsync, () => !IsBusy && Wizard.GpuIsNvidia);
            BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) { NotifyAdvanceChanged(); DownloadCommand.RaiseCanExecuteChanged(); BrowseCommand.RaiseCanExecuteChanged(); } } }

        private double _progress;
        public double Progress { get => _progress; private set => Set(ref _progress, value); }

        private string _status = "";
        public string Status { get => _status; private set => Set(ref _status, value); }

        private string _error = "";
        public string Error { get => _error; private set => Set(ref _error, value); }

        public bool HasSelection => Wizard.InstallerInfo != null;
        public string SelectedFileName => Wizard.InstallerInfo != null ? Path.GetFileName(Wizard.InstallerInfo.FilePath) : "";
        public string SelectedFileSize => Wizard.InstallerInfo?.FileSizeDisplay ?? "";
        public string DetectionSummary => Wizard.GpuIsNvidia
            ? "Detected: " + Wizard.Hardware.GpuName
            : "No NVIDIA GPU detected. Automatic download and installation are unavailable; browse an official package to inspect it.";

        public AsyncRelayCommand DownloadCommand { get; }
        public RelayCommand BrowseCommand { get; }

        public override bool CanAdvance => !IsBusy && HasSelection;
        public override bool CanGoBack => !IsBusy;

        public async Task PrepareLatestAsync()
        {
            if (IsBusy || !Wizard.GpuIsNvidia) return;
            await DownloadLatestAsync();
            if (string.IsNullOrEmpty(Error) && HasSelection && Wizard.Current == this)
            {
                var analyze = (AnalyzeStepViewModel)Wizard.StepFor(NvidiaWizardStep.Analyze);
                analyze.ContinueToComponents = true;
                Wizard.NextCommand.Execute(null);
            }
        }

        private async Task DownloadLatestAsync()
        {
            if (IsBusy) return;
            Error = "";
            IsBusy = true;
            Status = "Looking up the latest NVIDIA driver...";
            try
            {
                var info = await Wizard.DownloadService.ResolveLatestDriverAsync(Wizard.Hardware.GpuName);
                Status = $"Found {info.Version} — downloading...";

                string targetFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NvidiaDriverTool", "downloads");

                string path = await Wizard.DownloadService.DownloadAsync(
                    info.DownloadUrl, targetFolder, new Progress<double>(p => Progress = p < 0 ? 0 : p));

                SetInstaller(path, info.DownloadUrl);
                Status = $"Downloaded {info.Version}.";
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Status = "";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Browse()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select an NVIDIA driver installer",
                Filter = "Executable (*.exe)|*.exe",
            };
            if (dlg.ShowDialog() == true)
            {
                Error = "";
                SetInstaller(dlg.FileName, null);
                Status = "Selected " + Path.GetFileName(dlg.FileName);
            }
        }

        private void SetInstaller(string path, string downloadSource)
        {
            Wizard.ResetPackage();
            Wizard.InstallerInfo = Wizard.Validator.Inspect(path, downloadSource, DateTime.Now);
            Raise(nameof(HasSelection));
            Raise(nameof(SelectedFileName));
            Raise(nameof(SelectedFileSize));
            NotifyAdvanceChanged();
        }
    }
}
