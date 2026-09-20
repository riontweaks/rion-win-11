using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.Services
{
    public enum InstallOutcome
    {
        Installed,
        ClosedNoChange,
        NeverStarted,
    }

    public enum WatchPhase
    {
        Starting,
        Downloading,
        InstallerOpen,
        InstallingDriver,
        Finishing,
        Verifying,
    }

    public sealed class WatchProgress
    {
        public WatchPhase Phase { get; set; }
        public string Detail { get; set; }
        public double? Percent { get; set; }
    }

    /// <summary>
    /// Watches the AMD driver install like NVCleanstall watches the NVIDIA one. It follows the AMD
    /// installer <em>process set</em> and reads the live status straight out of the installer window
    /// (downloading / installing / % / "click Close"), only declaring completion once the installer
    /// has confirmed success (or the driver actually changed).
    /// </summary>
    public sealed class InstallerWatcher
    {
        // The AMD *installer* process set - these mean an install is actively in progress.
        private static readonly string[] InstallerNeedles =
        {
            "amdinstall", "installmanager", "radeoninstaller", "amdsoftwareinstaller",
            "amdcleanup", "atisetup", "amdinst",
        };

        // AMD Radeon Software (Adrenalin) - the user-facing app + its server. Launching it after an
        // install means the install SUCCEEDED (unless the user unticked "Launch AMD Software"). It is
        // NOT the installer, and it must be closed before we can change AMD services / registry.
        // Deliberately excludes atieclxx.exe (the driver's External Events client - leave it alone).
        private static readonly string[] AdrenalinNeedles =
        {
            "radeonsoftware", "radeonsettings", "amdrsserv", "cncmd", "cnext", "amddvr",
        };

        private static readonly string[] DriverNeedles = { "drvinst" };

        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _goneGrace = TimeSpan.FromSeconds(8);
        private readonly TimeSpan _confirmedGrace = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(120);

        public async Task<InstallOutcome> WatchAsync(
            Process bootstrapper,
            HardwareInfo before,
            IProgress<WatchProgress> onProgress,
            CancellationToken ct)
        {
            void Report(WatchPhase phase, string detail, double? percent) =>
                onProgress?.Report(new WatchProgress { Phase = phase, Detail = detail, Percent = percent });

            Report(WatchPhase.Starting, "Launching the AMD installer.", null);

            Stopwatch overall = Stopwatch.StartNew();
            DateTime? firstSeen = bootstrapper != null ? DateTime.UtcNow : null;
            DateTime? goneSince = null;
            bool installConfirmed = false;
            bool appLaunched = false;
            bool sawRealInstall = false;   // genuine in-progress state - progress %, drvinst, or "installing"

            // Radeon Software is usually already running, and the installer stops it mid-install and
            // relaunches it on success - so what matters is the transition, not the starting state.
            bool adrenalinWasRunning = AnyProcess(AdrenalinNeedles);

            while (!ct.IsCancellationRequested)
            {
                AmdInstallerStatus ui = AmdInstallerUiReader.Read();
                bool procAlive = IsAlive(bootstrapper) || AnyProcess(InstallerNeedles);
                bool driverStage = AnyProcess(DriverNeedles);
                bool present = ui.WindowFound || procAlive;

                // The installer only launches Radeon Software once the install has succeeded, so
                // this is the finish line - don't wait for the installer window to close too.
                bool adrenalinNow = AnyProcess(AdrenalinNeedles);
                bool adrenalinJustLaunched = adrenalinNow && !adrenalinWasRunning;
                adrenalinWasRunning = adrenalinNow;

                // firstSeen: the installer really ran. !driverStage: the driver store is done.
                if (adrenalinJustLaunched && firstSeen != null && !driverStage
                    && (installConfirmed || sawRealInstall || !present))
                {
                    appLaunched = true;
                    Report(WatchPhase.Verifying,
                        "AMD Software opened — validating the installed driver…", null);
                    break;
                }

                if (present)
                {
                    firstSeen ??= DateTime.UtcNow;
                    goneSince = null;

                    PhaseReading r = Classify(ui.Text, ui.Percent, driverStage);
                    if (r.Confirmed) installConfirmed = true;
                    if (r.RealInstall) sawRealInstall = true;
                    Report(r.Phase, r.Detail, r.Percent);
                }
                else if (firstSeen != null)
                {
                    goneSince ??= DateTime.UtcNow;
                    TimeSpan grace = installConfirmed ? _confirmedGrace : _goneGrace;
                    if (driverStage)
                    {
                        goneSince = null;
                        Report(WatchPhase.InstallingDriver, "Windows is finishing the driver installation…", null);
                        await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }
                    if (DateTime.UtcNow - goneSince.Value >= grace)
                        break;
                    Report(WatchPhase.Verifying, "Installer closed - checking the driver...", null);
                }
                else if (overall.Elapsed >= _startTimeout)
                {
                    return InstallOutcome.NeverStarted;
                }

                try { await Task.Delay(_pollInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
            }

            Report(WatchPhase.Verifying, "Checking the installed AMD driver...", null);
            ct.ThrowIfCancellationRequested();
            HardwareInfo after = null;
            for (int attempt = 0; attempt < 15; attempt++)
            {
                after = await Task.Run(() => new HardwareInspector().Inspect("1002"), ct).ConfigureAwait(false);
                if (DriverValidation.IsReady(after, "1002")) break;
                Report(WatchPhase.Verifying, "Waiting for Windows to report a healthy AMD driver…", null);
                await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
            }

            // Installed if: the installer said so, the driver version/date changed, or we saw a
            // real install in progress and every installer process then closed (a same-version
            // repair). EULA text alone never sets sawRealInstall.
            if (DriverValidation.IsReady(after, "1002")
                && (installConfirmed || appLaunched || DriverChanged(before, after) || (sawRealInstall && firstSeen != null)))
                return InstallOutcome.Installed;

            return firstSeen != null ? InstallOutcome.ClosedNoChange : InstallOutcome.NeverStarted;
        }

        /// <summary>Result of reading one poll of the AMD installer's state.</summary>
        public readonly struct PhaseReading
        {
            public WatchPhase Phase { get; init; }
            public string Detail { get; init; }
            public double? Percent { get; init; }
            /// <summary>Seen a genuine install-in-progress state (used to conclude a same-version repair).</summary>
            public bool RealInstall { get; init; }
            /// <summary>The installer told us on its own screen that it finished / needs a reboot.</summary>
            public bool Confirmed { get; init; }
        }

        /// <summary>
        /// Turns the installer window's status text + progress value into a phase. Pure and testable.
        /// Order matters: "install finished" / "restart" win first; then download; then <em>installing</em>
        /// (text OR a live 1-99% bar on a non-download screen - a moving bar can't happen on the EULA /
        /// chooser, so it beats a stale "Express Installation" label); then prep; then the chooser screen.
        /// </summary>
        public static PhaseReading Classify(string rawText, double? percent, bool driverStage)
        {
            string text = rawText?.ToLowerInvariant() ?? string.Empty;

            bool hasLiveProgress = percent.HasValue && percent.Value > 0 && percent.Value < 100;
            bool downloadingText = text.Contains("download");
            bool installingText = driverStage || text.Contains("installing") || text.Contains("applying")
                                  || text.Contains("configuring") || text.Contains("finaliz")
                                  || text.Contains("updating") || text.Contains("copying");
            bool prepText = text.Contains("prepar") || text.Contains("checking") || text.Contains("detecting")
                            || text.Contains("extract") || text.Contains("initializ");

            if (text.Contains("successfully") || text.Contains("welcome to the amd software") || text.Contains("installation complete"))
                return new PhaseReading { Phase = WatchPhase.Finishing, Detail = "Install finished - select Close in the AMD installer window.", Percent = percent, Confirmed = true };

            if (text.Contains("restart is required") || text.Contains("restart your") || text.Contains("reboot"))
                return new PhaseReading { Phase = WatchPhase.Finishing, Detail = "Windows needs to restart to finish. Reboot, then reopen this app.", Percent = percent, Confirmed = true };

            if (downloadingText && percent.HasValue)
                return new PhaseReading { Phase = WatchPhase.Downloading, Detail = "AMD is downloading the driver package set and unpacking it.", Percent = percent, RealInstall = true };

            if (installingText || (hasLiveProgress && !downloadingText && !prepText))
                return new PhaseReading { Phase = WatchPhase.InstallingDriver, Detail = "AMD is installing the packages. The screen may flicker or go black - this is normal.", Percent = percent, RealInstall = true };

            if (prepText)
                return new PhaseReading { Phase = WatchPhase.Starting, Detail = "Unpacking the AMD Software package and checking your system.", Percent = percent };

            return new PhaseReading { Phase = WatchPhase.InstallerOpen, Detail = "In the AMD installer window, choose Express installation to continue.", Percent = null };
        }

        /// <summary>True if the AMD <em>installer</em> (not Radeon Software) is still around - used as a
        /// safety check before touching services / registry after a supposedly-finished install.</summary>
        public static bool AmdInstallerStillRunning()
        {
            if (AnyProcess(InstallerNeedles) || AnyProcess(DriverNeedles))
                return true;
            try { return AmdInstallerUiReader.Read().WindowFound; }
            catch { return false; }
        }

        /// <summary>True if AMD Radeon Software (Adrenalin) is running - it auto-launches after a
        /// successful install and holds the AMD services / registry open.</summary>
        public static bool AdrenalinRunning() => AnyProcess(AdrenalinNeedles);

        /// <summary>
        /// Force-closes the AMD installer AND Radeon Software so the post-install service / registry
        /// changes can be applied cleanly. Never touches <c>drvinst.exe</c> or the driver's own
        /// <c>atieclxx.exe</c>. Best-effort; returns the process names it killed.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<string> ForceCloseAmdPostInstall()
        {
            var killed = new System.Collections.Generic.List<string>();
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { return killed; }

            try
            {
                foreach (Process p in all)
                {
                    string name;
                    try { name = p.ProcessName; }
                    catch { continue; }
                    if (string.IsNullOrEmpty(name))
                        continue;

                    string lower = name.ToLowerInvariant();
                    bool target = InstallerNeedles.Any(n => lower.Contains(n))
                                  || AdrenalinNeedles.Any(n => lower.Contains(n));
                    if (!target)
                        continue;

                    try
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(4000);
                        killed.Add(name);
                    }
                    catch { /* already gone / access denied - best effort */ }
                }
            }
            finally
            {
                foreach (Process p in all)
                {
                    try { p.Dispose(); } catch { /* ignore */ }
                }
            }

            return killed;
        }

        public static bool DriverChanged(HardwareInfo before, HardwareInfo after)
        {
            if (after == null)
                return false;

            return !Same(before?.DriverVersion, after.DriverVersion)
                || !Same(before?.DriverDate, after.DriverDate)
                || !Same(before?.AmdSoftwareVersion, after.AmdSoftwareVersion)
                || (after.AmdSoftwareDetected && !(before?.AmdSoftwareDetected ?? false));
        }

        private static bool Same(string a, string b) =>
            string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

        private static bool IsAlive(Process process)
        {
            if (process == null)
                return false;
            try { return !process.HasExited; }
            catch { return false; }
        }

        private static bool AnyProcess(string[] needles)
        {
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { return false; }

            try
            {
                foreach (Process p in all)
                {
                    string name;
                    try { name = p.ProcessName; }
                    catch { continue; }
                    if (string.IsNullOrEmpty(name))
                        continue;

                    string lower = name.ToLowerInvariant();
                    if (needles.Any(n => lower.Contains(n)))
                        return true;
                }
                return false;
            }
            finally
            {
                foreach (Process p in all)
                {
                    try { p.Dispose(); } catch { /* ignore */ }
                }
            }
        }
    }
}
