using System;
using RadeonSoftwareSlimmer.Services;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Optimize
{
    /// <summary>Real bidirectional Windows Update toggle for the Windows tab's Health section (the
    /// existing "windows-update-fix" in <see cref="FixCatalog"/> is enable-only). CustomApply/
    /// CustomRevert because it spans three services.
    ///
    /// Honest limitation, stated here and in the catalog Summary text: the Windows Update Medic
    /// Service (WaaSMedicSvc) exists specifically to self-heal disabled update components and may
    /// silently re-enable wuauserv on its own schedule — this toggle isn't guaranteed to stick
    /// long-term the way the other tweaks are.</summary>
    internal static class WindowsUpdateToggle
    {
        private static readonly (string Svc, int OnStart, int OffStart)[] Services =
        {
            ("wuauserv", 3, 4),
            ("BITS", 3, 4),
            ("UsoSvc", 3, 4),
        };

        public static bool? IsDisabled()
        {
            try
            {
                var reg = new WindowsRegistry();
                int? start = ServiceTrimCatalog.GetCurrentStart(reg, "wuauserv");
                return start == 4;
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not read Windows Update toggle state");
                return null;
            }
        }

        public static bool Apply()
        {
            try
            {
                var reg = new WindowsRegistry();
                foreach (var (svc, _, offStart) in Services)
                    ServiceTrimCatalog.SetStart(reg, svc, offStart, null, "Windows");
                StaticViewModel.AddLogMessage("Disabled Windows Update services. Note: Windows Update Medic Service (WaaSMedicSvc) may silently re-enable them.");
                return true;
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not disable Windows Update");
                return false;
            }
        }

        public static bool Revert()
        {
            try
            {
                var reg = new WindowsRegistry();
                foreach (var (svc, onStart, _) in Services)
                    ServiceTrimCatalog.SetStart(reg, svc, onStart, null, "Windows");
                StaticViewModel.AddLogMessage("Re-enabled Windows Update services.");
                return true;
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not re-enable Windows Update");
                return false;
            }
        }
    }
}
