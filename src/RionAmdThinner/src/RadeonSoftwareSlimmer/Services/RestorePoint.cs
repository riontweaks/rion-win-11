using System;
using System.Diagnostics;
using System.IO;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    public static class RestorePoint
    {
        /// <summary>
        /// Best-effort System Restore checkpoint via PowerShell. Returns true on success.
        /// System Protection must be enabled on the system drive; failures are logged, not thrown.
        /// </summary>
        public static bool TryCreate(string description)
        {
            try
            {
                string powershell = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe");

                if (!File.Exists(powershell))
                    throw new FileNotFoundException("Windows PowerShell is missing from its system location.", powershell);

                string script =
                    "try { Checkpoint-Computer -Description '" + Sanitize(description) +
                    "' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; exit 0 } catch { exit 1 }";

                ProcessStartInfo startInfo = new ProcessStartInfo(powershell)
                {
                    Arguments = "-NoProfile -NonInteractive -Command \"" + script + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                StaticViewModel.AddLogMessage("Creating a system restore point...");
                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit(120000);
                    if (!process.HasExited)
                    {
                        StaticViewModel.AddLogMessage("Restore point timed out; continuing without one");
                        return false;
                    }

                    if (process.ExitCode == 0)
                    {
                        StaticViewModel.AddLogMessage("Restore point created");
                        return true;
                    }

                    StaticViewModel.AddLogMessage(
                        "Could not create a restore point (System Protection may be off). Continuing.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddLogMessage(ex, "Could not create a restore point");
                return false;
            }
        }

        private static string Sanitize(string value) =>
            string.IsNullOrEmpty(value) ? "Rion AMD Thinner" : value.Replace("'", " ");
    }
}
