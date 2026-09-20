using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels.Wizard
{
    /// <summary>
    /// Standalone "Post-Install" tool reached from the rail (not part of the wizard flow). It hosts the
    /// RadeonSoftwareSlimmer post-install engine — services, scheduled tasks, host processes, installed
    /// entries, temp files — and applies changes immediately, the way RadeonSoftwareSlimmer's own
    /// Post-Install tab does. Available any time, whether or not a driver was installed this session.
    /// </summary>
    public sealed class PostInstallToolViewModel : ObservableObject
    {
        private readonly WizardViewModel _wizard;

        public PostInstallToolViewModel(WizardViewModel wizard)
        {
            _wizard = wizard;
            ReloadCommand = new AsyncRelayCommand(() => Post.LoadOrRefreshAsync(true));
            StopRadeonSoftwareCommand = new AsyncRelayCommand(StopRadeonSoftwareAsync);
        }

        public string Title => "Post-Install";

        public string Blurb =>
            "Trim and control AMD's installed footprint at any time — background services, scheduled tasks, "
            + "host processes, installed entries and leftover logs. Changes here apply immediately; a restart "
            + "is recommended afterwards.";

        /// <summary>The shared RadeonSoftwareSlimmer post-install engine (same instance the wizard uses).</summary>
        public PostInstallViewModel Post => _wizard.Post;

        public string Gpu => _wizard.Hardware?.GpuName;
        public string DriverVersion => _wizard.Hardware?.DriverVersion;
        public string AmdSoftwareVersion => _wizard.Hardware?.AmdSoftwareVersion;

        public AsyncRelayCommand ReloadCommand { get; }
        public AsyncRelayCommand StopRadeonSoftwareCommand { get; }

        /// <summary>Loads the panel the first time the section is shown.</summary>
        public async Task EnsureLoadedAsync()
        {
            await Post.LoadOrRefreshAsync(false);
        }

        private async Task StopRadeonSoftwareAsync()
        {
            await Task.Run(() => Post.HostService.StopRadeonSoftware());
            AuditLog.Action("Stop Radeon Software", "PostInstallTool", "host processes",
                newValue: "stopped", result: "OK", reversible: true);
            await Post.LoadOrRefreshAsync(false);
        }
    }
}
