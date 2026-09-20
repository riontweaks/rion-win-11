using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>What the AMD installer window is currently showing, read live via UI Automation.</summary>
    public sealed class AmdInstallerStatus
    {
        public bool WindowFound { get; set; }
        /// <summary>Best status line found, e.g. "Downloading packages", "Installing", "Restart required".</summary>
        public string Text { get; set; }
        /// <summary>Progress 0..100 if the window exposes a progress bar.</summary>
        public double? Percent { get; set; }
        /// <summary>e.g. "09:48" if the window shows a time-remaining string.</summary>
        public string TimeRemaining { get; set; }
    }

    /// <summary>
    /// Reads the live status out of the running "AMD Software Installer" window so the Installation
    /// Monitor can mirror exactly what the user sees (downloading / installing / % / time left)
    /// instead of a generic "running". Best-effort: UI Automation can and does throw, and AMD can
    /// change their window at any time - every failure just yields an empty status.
    /// </summary>
    public static class AmdInstallerUiReader
    {
        private static readonly string[] TitleNeedles =
        {
            "AMD Software Installer", "AMD Software: Adrenalin", "AMD Software Install",
            "Radeon Software Installer", "AMD Radeon Software Installer", "AMD Software Setup",
        };

        private static readonly string[] StatusKeywords =
        {
            "download", "install", "prepar", "checking", "check for", "detect", "extract",
            "finaliz", "cleanup", "clean up", "complet", "restart", "reboot", "configuring", "applying",
        };

        // Live-status lines start with one of these; used to pick the current line over stale ones.
        private static readonly string[] ActionWords =
        {
            "installing", "downloading", "preparing", "checking", "detecting", "extracting",
            "finalizing", "finalising", "cleaning", "configuring", "applying", "updating",
            "copying", "initializing", "initialising", "verifying",
        };

        public static AmdInstallerStatus Read() => ReadVendor(false);
        public static AmdInstallerStatus ReadNvidia() => ReadVendor(true);

        public static bool IsInstallerWindow(string title, string processName, bool nvidia)
        {
            if (nvidia)
                return (title.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0
                    && (title.IndexOf("install", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0))
                    && !processName.Equals("NVIDIA app", StringComparison.OrdinalIgnoreCase);
            if (processName.Equals("RadeonSoftware", StringComparison.OrdinalIgnoreCase)
                || processName.Equals("RadeonSettings", StringComparison.OrdinalIgnoreCase)) return false;
            return TitleNeedles.Any(t => title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static AmdInstallerStatus ReadVendor(bool nvidia)
        {
            var result = new AmdInstallerStatus();
            try
            {
                AutomationElement root = AutomationElement.RootElement;
                if (root == null)
                    return result;

                AutomationElement window = root
                    .FindAll(TreeScope.Children, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .FirstOrDefault(w =>
                    {
                        string name;
                        try { name = w.Current.Name ?? string.Empty; } catch { return false; }
                        try
                        {
                            using var process = System.Diagnostics.Process.GetProcessById(w.Current.ProcessId);
                            return IsInstallerWindow(name, process.ProcessName, nvidia);
                        }
                        catch { return false; }
                    });

                if (window == null)
                    return result;

                result.WindowFound = true;

                AutomationElement progress = window.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ProgressBar));
                if (progress != null
                    && progress.TryGetCurrentPattern(RangeValuePattern.Pattern, out object rvpObj)
                    && rvpObj is RangeValuePattern rvp)
                {
                    double v = rvp.Current.Value;
                    double min = rvp.Current.Minimum;
                    double max = rvp.Current.Maximum;
                    if (max > min)
                    {
                        double pct = (v - min) / (max - min) * 100.0;
                        result.Percent = Math.Max(0, Math.Min(100, Math.Round(pct)));
                    }
                }

                var texts = window.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                    .Cast<AutomationElement>()
                    .Select(e => { try { return e.Current.Name; } catch { return null; } })
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToList();

                string timeLine = texts.FirstOrDefault(s => s.IndexOf("remaining", StringComparison.OrdinalIgnoreCase) >= 0);
                if (timeLine != null)
                {
                    Match m = Regex.Match(timeLine, @"\d{1,2}:\d{2}(:\d{2})?");
                    if (m.Success)
                        result.TimeRemaining = m.Value;
                }

                // Prefer a line that *starts* with a progress verb ("Installing ...", "Downloading ...")
                // - that's the live status line. AMD keeps hidden wizard pages ("Express Installation",
                // "Recommended") in the tree, and those would otherwise win a plain keyword search and
                // make the monitor think we're still on the chooser screen.
                result.Text =
                    texts.LastOrDefault(s => ActionWords.Any(w => s.StartsWith(w, StringComparison.OrdinalIgnoreCase)))
                    ?? texts.LastOrDefault(s => StatusKeywords.Any(k => s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    ?? texts.FirstOrDefault(s => s.Length > 3 && s.Length < 70
                                                 && s.IndexOf(':') < 0
                                                 && TitleNeedles.All(t => s.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0));
            }
            catch
            {
                // UI Automation is flaky by nature - treat any failure as "no status this tick".
            }

            return result;
        }
    }
}
