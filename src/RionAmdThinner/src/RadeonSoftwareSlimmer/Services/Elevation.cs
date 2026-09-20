using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    public static class Elevation
    {
        public static bool IsElevated { get; } = GetIsElevated();

        private static bool GetIsElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void RelaunchElevated()
        {
            // Hosted inside "Rion Win 11", which ships a requireAdministrator manifest, so the
            // whole process is already elevated and this path is unreachable in practice.
            // Never call Application.Current.Shutdown() here — that would tear down the shell
            // and the other two tools. If somehow run un-elevated (dev), just tell the user.
            if (IsElevated)
                return;

            MessageBox.Show(
                "Rion Win 11 needs to run as administrator for this action. Close it and relaunch from an elevated shortcut (right-click → Run as administrator).",
                "Administrator required", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
