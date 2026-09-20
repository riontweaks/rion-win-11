using System;
using System.Diagnostics;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Optimize
{
    /// <summary>Small synchronous shell helpers for command-based <see cref="SystemTweak"/> entries
    /// (settings with no registry representation, e.g. BCD options). Kept synchronous — same call
    /// shape as <see cref="TweakService"/>'s registry reads — since these are fast, local commands;
    /// long-running scripted work still goes through the async <see cref="ShellRunner"/>.</summary>
    internal static class CommandTweakHelper
    {
        /// <summary>Reads a bcdedit Yes/No option from the current boot entry. Null if bcdedit
        /// itself couldn't be queried; false if the option is absent (BCD's own default is No).</summary>
        public static bool? IsBcdOptionYes(string bcdOptionName)
        {
            try
            {
                var psi = new ProcessStartInfo("bcdedit.exe", "/enum {current}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    foreach (string line in output.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith(bcdOptionName, StringComparison.OrdinalIgnoreCase))
                            return trimmed.IndexOf("Yes", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not query bcdedit option " + bcdOptionName);
                return null;
            }
        }

        /// <summary>Reads a powercfg AC-power setting's current index under the active scheme.
        /// Null if it can't be queried/parsed.</summary>
        public static int? QueryPowercfgAcIndex(string subGuid, string settingGuid)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg.exe", $"/query SCHEME_CURRENT {subGuid} {settingGuid}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    foreach (string line in output.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        int idx = trimmed.IndexOf("Current AC Power Setting Index:", StringComparison.OrdinalIgnoreCase);
                        if (idx < 0) continue;
                        string hex = trimmed.Substring(idx + "Current AC Power Setting Index:".Length).Trim();
                        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(hex.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int value))
                            return value;
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not query powercfg setting " + settingGuid);
                return null;
            }
        }

        /// <summary>Runs one command line synchronously (used for a tweak's Apply/Revert command,
        /// which are short, local, and need to complete before the toggle UI reports success).</summary>
        public static bool RunCommand(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (Process proc = Process.Start(psi))
                {
                    proc.WaitForExit(15000);
                    return proc.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not run command: " + command);
                return false;
            }
        }
    }
}
