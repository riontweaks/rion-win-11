using System;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    public sealed class SelectDriverStepViewModel : WizardStepViewModel
    {
        public SelectDriverStepViewModel(WizardViewModel wizard)
            : base(wizard, WizardStep.SelectDriver, "Select Driver", "")
        {
            BrowseCommand = new RelayCommand(Browse);
            ClearCommand = new RelayCommand(Clear);
            ValidateCommand = new RelayCommand(Validate, () => !string.IsNullOrWhiteSpace(Pre.InstallerFiles.InstallerFile));
            DownloadAutoDetectCommand = new AsyncRelayCommand(Pre.DownloadLatestDriverAsync, () => !Pre.IsDownloading);
            DownloadFullPackageCommand = new AsyncRelayCommand(Pre.DownloadGpuSeriesPackageAsync, () => !Pre.IsDownloading);

            Pre.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PreInstallViewModel.IsDownloading))
                {
                    Raise(nameof(IsDownloading));
                    NotifyAdvanceChanged();
                }
                if (e.PropertyName == nameof(PreInstallViewModel.DownloadProgress))
                    Raise(nameof(DownloadProgress));
            };
            Pre.InstallerFiles.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Models.PreInstall.InstallerFilesModel.InstallerFile))
                {
                    Info = null;
                    Wizard.InstallerInfo = null;
                    Wizard.Session.InstallerInfo = null;
                    Wizard.Session.OriginalInstallerPath = null;
                    PreparationStatus = "";
                }
                Raise(nameof(InstallerPath));
                ValidateCommand.RaiseCanExecuteChanged();
                NotifyAdvanceChanged();
            };
        }

        private PreInstallViewModel Pre => Wizard.Pre;

        public bool PreferDownload { get; set; }

        public HardwareInfo Hardware => Wizard.Hardware;

        public string InstallerPath => Pre.InstallerFiles.InstallerFile;
        private bool preparing;
        private double preparationProgress;
        private string preparationStatus = "";
        public string PreparationStatus { get => preparationStatus; private set => Set(ref preparationStatus, value); }
        public bool IsDownloading => preparing || Pre.IsDownloading;
        public double DownloadProgress => preparing ? preparationProgress : Pre.DownloadProgress;
        public override bool CanGoBack => !IsDownloading;

        public async System.Threading.Tasks.Task PrepareLatestAsync()
        {
            if (IsDownloading || !Hardware.IsAmdGpu) return;
            preparing = true; Raise(nameof(IsDownloading)); NotifyAdvanceChanged();
            try
            {
                PreparationStatus = "Finding the compatible AMD package…";
                var downloader = new DriverDownloadService();
                var package = await downloader.ResolveGpuSeriesPackageAsync(Hardware.GpuName);
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RionWin11", "downloads", "AMD");
                PreparationStatus = "Downloading the official package…";
                var progress = new Progress<double>(p => { preparationProgress = p < 0 ? 0 : p; Raise(nameof(DownloadProgress)); });
                string file = await downloader.DownloadAsync(package.InstallerUrl, folder, progress, package.ProductPageUrl);
                Pre.InstallerFiles.InstallerFile = file;
                Validate();
                if (!HistoricalDriverInstaller.Trusted("AMD", Info)) throw new InvalidOperationException("The package does not have a verified AMD signature.");
                PreparationStatus = "Download verified. Opening package analysis…";
            }
            catch (Exception ex) { PreparationStatus = "Preparation failed: " + ex.Message; return; }
            finally { preparing = false; Raise(nameof(IsDownloading)); NotifyAdvanceChanged(); }
            if (Wizard.Current != this) return;
            Wizard.NextCommand.Execute(null);
            var analyze = (AnalyzeStepViewModel)Wizard.StepFor(WizardStep.Analyze);
            await analyze.AnalyzePreparedAsync();
            if (analyze.CanAdvance && Wizard.Current == analyze) Wizard.NextCommand.Execute(null);
        }

        private DriverInstallerInfo _info;
        public DriverInstallerInfo Info
        {
            get => _info;
            private set
            {
                Set(ref _info, value);
                Raise(nameof(HasInfo));
                Raise(nameof(SignatureText));
                Raise(nameof(TrustWarning));
                NotifyAdvanceChanged();
            }
        }

        public bool HasInfo => _info != null;

        public string SignatureText
        {
            get
            {
                if (_info == null) return null;
                switch (_info.SignatureStatus)
                {
                    case SignatureStatus.Valid: return "Signed by " + _info.Publisher + " (AMD) — valid";
                    case SignatureStatus.ValidButNotAmd: return "Signed by " + _info.Publisher + " — valid, but not AMD";
                    case SignatureStatus.Unsigned: return "Not digitally signed";
                    case SignatureStatus.Invalid: return "Signature present but did NOT verify";
                    default: return "Signature could not be checked";
                }
            }
        }

        public bool TrustWarning =>
            _info != null && !HistoricalDriverInstaller.Trusted("AMD", _info);

        public RelayCommand BrowseCommand { get; }
        public RelayCommand ClearCommand { get; }
        public RelayCommand ValidateCommand { get; }
        public AsyncRelayCommand DownloadAutoDetectCommand { get; }
        public AsyncRelayCommand DownloadFullPackageCommand { get; }

        public override bool CanAdvance =>
            _info != null && !IsDownloading &&
            HistoricalDriverInstaller.Trusted("AMD", _info) &&
            System.IO.File.Exists(_info.FilePath);

        public override string AdvanceLabel => "Analyze Installer";

        private void Browse() => Pre.BrowseForInstallerFile();

        private void Clear()
        {
            Pre.InstallerFiles.InstallerFile = string.Empty;
            Pre.InstallerFiles.ExtractedInstallerDirectory = string.Empty;
            Info = null;
        }

        private void Validate()
        {
            string path = Pre.InstallerFiles.InstallerFile;
            string source = Pre.InstallerFiles.InstallerFile;   // download source is tracked elsewhere; keep simple
            DriverInstallerInfo info = Wizard.Validator.Inspect(path, source);
            Wizard.InstallerInfo = info;
            Wizard.Session.InstallerInfo = info;
            Wizard.Session.OriginalInstallerPath = path;
            Info = info;
            PreparationStatus = HistoricalDriverInstaller.Trusted("AMD", info)
                ? "AMD signature verified. Ready to analyze."
                : "Validation failed: choose a package with a verified AMD publisher signature.";

            AuditLog.Action("Validate installer", "InstallerValidation", path,
                newValue: info.SignatureStatus + " / " + (info.OriginalHashSha256 ?? "").Substring(0, System.Math.Min(12, info.OriginalHashSha256?.Length ?? 0)),
                result: info.SignatureStatus == SignatureStatus.Error ? "Error" : "OK", reversible: false);
        }
    }
}
