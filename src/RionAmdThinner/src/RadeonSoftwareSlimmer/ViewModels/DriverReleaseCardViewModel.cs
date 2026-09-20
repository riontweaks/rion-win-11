using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels
{
    public sealed class DriverReleaseCardViewModel : ObservableObject
    {
        private readonly DriverReleaseService service;
        private readonly string gpuName;
        private readonly Func<DriverInstallerInfo> selectedInstaller;
        private DriverReleaseSnapshot snapshot;
        private DateTimeOffset lastAttempt;
        private bool busy, failed;
        private string status = "Checking the official website when this page opens…";
        public DriverReleaseCardViewModel(string vendor, string gpuName, Func<DriverInstallerInfo> selectedInstaller = null,
            DriverReleaseService service = null, Func<Task> prepareInstaller = null)
        {
            Vendor = vendor; this.gpuName = gpuName; this.selectedInstaller = selectedInstaller;
            this.service = service ?? new DriverReleaseService();
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(true), () => !IsBusy);
            OpenSourceCommand = new RelayCommand(() => Open(SourceUrl));
            OpenNotesCommand = new RelayCommand(() => Open(snapshot?.Latest.NotesUrl), () => snapshot != null);
            OpenAlternativeCommand = new RelayCommand(() => Open(snapshot?.Alternative?.NotesUrl), () => snapshot?.Alternative != null);
            Recommendations = DriverRecommendationCatalog.ForVendor(vendor);
            recommendation = Recommendations.Count > 0 ? Recommendations[0] : null;
            OpenRecommendationNotesCommand = new RelayCommand(() => Open(SelectedRecommendation?.OfficialUrl), () => SelectedRecommendation != null);
            OpenRecommendationEvidenceCommand = new RelayCommand(() =>
            {
                string url = SelectedRecommendation?.EvidenceUrl;
                if (!DriverRecommendationCatalog.IsEvidenceUrl(Vendor, url)) return;
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { Status = "Could not open your browser. Evidence page: " + url; }
            }, () => SelectedRecommendation != null);
            CanPrepareInstaller = prepareInstaller != null && !string.IsNullOrWhiteSpace(gpuName);
            InstallCommand = new RelayCommand(async () =>
            {
                if (preparing || !CanPrepareInstaller) return;
                preparing = true; InstallCommand.RaiseCanExecuteChanged(); InstallRecommendationCommand?.RaiseCanExecuteChanged(); Raise(nameof(CanSelectRecommendation));
                try { await prepareInstaller(); }
                catch (Exception ex) { Status = "Package preparation failed: " + ex.Message; }
                finally { preparing = false; InstallCommand.RaiseCanExecuteChanged(); InstallRecommendationCommand?.RaiseCanExecuteChanged(); Raise(nameof(CanSelectRecommendation)); RefreshLocalPackage(); }
            }, () => CanPrepareInstaller && !preparing);
            InstallRecommendationCommand = new RelayCommand(async () => await InstallRecommendationAsync(),
                () => !preparing && SelectedRecommendation != null && !string.IsNullOrWhiteSpace(gpuName));
            CancelRecommendationCommand = new RelayCommand(() => historyCancellation?.Cancel(), () => historyCancellation != null);
        }
        public string Vendor { get; }
        public System.Collections.Generic.IReadOnlyList<DriverRecommendation> Recommendations { get; }
        private DriverRecommendation recommendation;
        public DriverRecommendation SelectedRecommendation
        {
            get => recommendation;
            set
            {
                if (preparing || value != null && !System.Linq.Enumerable.Contains(Recommendations, value)) return;
                Set(ref recommendation, value);
                OpenRecommendationNotesCommand?.RaiseCanExecuteChanged();
                OpenRecommendationEvidenceCommand?.RaiseCanExecuteChanged();
                InstallRecommendationCommand?.RaiseCanExecuteChanged();
            }
        }
        public string RecommendationsReviewed => "Last reviewed: " + DriverRecommendationCatalog.ReviewedOn.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
        public RelayCommand OpenRecommendationNotesCommand { get; }
        public RelayCommand OpenRecommendationEvidenceCommand { get; }
        private bool preparing;
        private CancellationTokenSource historyCancellation;
        private string historyStatus = "Downloads the selected version and opens the official installer. Complete its license and installation prompts. This installs the full vendor package; existing custom component selections do not apply.";
        public string HistoryStatus { get => historyStatus; private set => Set(ref historyStatus, value); }
        public bool CanSelectRecommendation => !preparing;
        public RelayCommand InstallRecommendationCommand { get; }
        public RelayCommand CancelRecommendationCommand { get; }
        public async Task InstallRecommendationAsync()
        {
            if (preparing || SelectedRecommendation == null || string.IsNullOrWhiteSpace(gpuName)) return;
            var selected = SelectedRecommendation;
            preparing = true;
            using var cancellation = new CancellationTokenSource();
            historyCancellation = cancellation;
            InstallCommand.RaiseCanExecuteChanged(); InstallRecommendationCommand.RaiseCanExecuteChanged(); CancelRecommendationCommand.RaiseCanExecuteChanged(); Raise(nameof(CanSelectRecommendation));
            try
            {
                await new HistoricalDriverInstaller().DownloadAndLaunchAsync(selected, gpuName,
                    new Progress<string>(message => HistoryStatus = message), cancellation.Token,
                    () => { historyCancellation = null; CancelRecommendationCommand.RaiseCanExecuteChanged(); });
            }
            catch (OperationCanceledException) { HistoryStatus = "Download canceled. No installer was launched."; }
            catch (Exception ex) { HistoryStatus = "Could not complete " + selected.Version + ": " + ex.Message; }
            finally
            {
                historyCancellation = null; preparing = false;
                InstallCommand.RaiseCanExecuteChanged(); InstallRecommendationCommand.RaiseCanExecuteChanged(); CancelRecommendationCommand.RaiseCanExecuteChanged(); Raise(nameof(CanSelectRecommendation));
            }
        }
        public bool CanPrepareInstaller { get; }
        public RelayCommand InstallCommand { get; }
        public string InstallHint => CanPrepareInstaller
            ? "Install starts a compatible-package download and opens package selection. Review components and services before launching setup. The selected compatible release may differ from this overview."
            : "Detect a supported GPU to download automatically, or browse a package in Select Driver for inspection.";
        public string Title => "Latest from " + Vendor;
        public string LogoUri => "/AmdDriverManager;component/Assets/DriverReleases/" + (Vendor == "AMD" ? "amd.png" : "nvidia.png");
        public string SourceUrl => snapshot?.SourceUrl ?? (Vendor == "AMD" ? DriverReleaseService.AmdIndex : DriverReleaseService.NvidiaIndex);
        public string SourceLabel => Vendor == "AMD" ? "amd.com" : "nvidia.com";
        public string SourceTrust => snapshot == null ? "Official source · " + SourceLabel : "Release source · official " + SourceLabel + " (HTTPS)";
        public bool HasWarning => failed;
        public string Scope => snapshot?.Scope ?? "Official Windows 11 release listing";
        public string Version => snapshot?.Latest.Version ?? "—";
        public string VersionLabel => Version + (System.Linq.Enumerable.Any(Recommendations, r => r.IsOptimal && r.Version == Version) ? " · Optimal" : "");
        public string Channel => snapshot?.Latest.Channel ?? "Release information";
        public string Released => snapshot == null ? "Date not yet available" : "Released " + snapshot.Latest.ReleaseDate.ToString("dd MMM yyyy");
        public string Certification => snapshot?.Latest.Certification ?? "Certification not yet available";
        public string Size => string.IsNullOrEmpty(snapshot?.Latest.Size) ? "" : "Package size · " + snapshot.Latest.Size;
        public string Freshness => IsBusy ? "Checking live" : failed ? snapshot == null ? "Unavailable" : "Last known · refresh failed" : snapshot == null ? "Not checked" : "Checked live";
        public string CheckedAt => snapshot == null ? "No successful check yet" : "Last success · " + snapshot.CheckedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm zzz");
        public string Status { get => status; private set => Set(ref status, value); }
        public bool IsBusy { get => busy; private set { Set(ref busy, value); Raise(nameof(Freshness)); RefreshCommand.RaiseCanExecuteChanged(); } }
        public bool HasAlternative => snapshot?.Alternative != null;
        public string AlternativeTitle => snapshot?.Alternative == null ? "" : Vendor == "AMD" ? "AMD recommended release" : "Latest Studio release";
        public string AlternativeSummary => snapshot?.Alternative == null ? "" : $"{snapshot.Alternative.Version}"
            + (System.Linq.Enumerable.Any(Recommendations, r => r.IsOptimal && r.Version == snapshot.Alternative.Version) ? " · Optimal" : "")
            + $"  ·  {snapshot.Alternative.ReleaseDate:dd MMM yyyy}  ·  {snapshot.Alternative.Certification}";
        public string Stability => snapshot == null ? "Stability cannot be assessed until the vendor listing is available."
            : snapshot.Latest.Channel == "Optional" ? "AMD marks this release Optional. A newer release is not necessarily the best choice for every game; review known issues."
            : "Vendor certification is not a guarantee of stability on this PC. Check the release notes and known issues for your GPU and applications.";
        public string Compatibility => "Release overview, not an install recommendation. Confirm supported GPUs and notebook/OEM requirements in the release notes.";
        public string Signature => "Installer signature · not verified by a website lookup. The selected package is checked separately before installation.";
        public string SelectedPackage
        {
            get
            {
                var info = selectedInstaller?.Invoke();
                if (info == null) return "No local installer selected.";
                // Keep local package identity separate: it may not be the online release shown above.
                string signature = info.SignatureStatus switch
                {
                    SignatureStatus.Valid => "Valid Authenticode signature (AMD publisher)",
                    SignatureStatus.ValidButNotAmd => "Valid Authenticode signature (non-AMD publisher; confirm it matches " + Vendor + ")",
                    SignatureStatus.Invalid => "Invalid signature",
                    SignatureStatus.Unsigned => "Unsigned",
                    SignatureStatus.Error => "Signature could not be checked",
                    _ => "Not checked"
                };
                return $"Selected file · {Path.GetFileName(info.FilePath)}\nRecorded selection check · {signature}\nPublisher · {info.Publisher ?? "not available"}\nSHA-256 · {info.OriginalHashSha256 ?? "not checked"}\nThis file may differ from the online release above.";
            }
        }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand OpenSourceCommand { get; }
        public RelayCommand OpenNotesCommand { get; }
        public RelayCommand OpenAlternativeCommand { get; }
        public void RefreshLocalPackage() => Raise(nameof(SelectedPackage));
        public async Task RefreshAsync(bool force = false)
        {
            RefreshLocalPackage();
            if (IsBusy || !force && DateTimeOffset.UtcNow - lastAttempt < TimeSpan.FromMinutes(15)) return;
            lastAttempt = DateTimeOffset.UtcNow;
            IsBusy = true;
            Status = "Reading release details from " + SourceLabel + "…";
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                snapshot = await service.ReadAsync(Vendor, gpuName, timeout.Token);
                failed = false;
                Status = "Refreshes every 15 minutes while visible. Refresh now checks immediately.";
            }
            catch (Exception ex)
            {
                failed = true;
                string reason = ex is OperationCanceledException ? "The request timed out." : "The website could not be read or its format changed.";
                Status = reason + (snapshot == null ? " No release version is being assumed." : " Showing the last successful lookup; it may be out of date.") + " Retry or open the official website.";
            }
            finally
            {
                IsBusy = false;
                Raise(nameof(VersionLabel));
                Raise(nameof(HasWarning)); Raise(nameof(SourceTrust));
                foreach (string property in new[] { nameof(SourceUrl), nameof(Scope), nameof(Version), nameof(Channel), nameof(Released), nameof(Certification), nameof(Size), nameof(Freshness), nameof(CheckedAt), nameof(HasAlternative), nameof(AlternativeTitle), nameof(AlternativeSummary), nameof(Stability) }) Raise(property);
                OpenNotesCommand.RaiseCanExecuteChanged(); OpenAlternativeCommand.RaiseCanExecuteChanged();
            }
        }
        private void Open(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !DriverReleaseService.IsOfficial(Vendor, uri)) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { Status = "Could not open your browser. Official page: " + uri.AbsoluteUri; }
        }
    }
}
