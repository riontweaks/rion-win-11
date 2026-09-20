using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Optimize
{
    /// <summary>The Windows tab's Health section — Windows Update
    /// toggles, reusing the same SystemTweak/TweakService machinery as General/Network rather than
    /// inventing a separate toggle mechanism. Not part of TweakCatalog.All — these are surfaced
    /// only on the Windows tab, not the General tweak grid.</summary>
    public static class WindowsHealthCatalog
    {
        public static SystemTweak WindowsUpdateOff { get; } = new SystemTweak
        {
            Id = "windows-update-off",
            Title = "Disable Windows Update",
            Category = TweakCategory.System,
            Summary = "Disables the Windows Update, BITS, and Update Orchestrator services. The Windows Update Medic Service (WaaSMedicSvc) exists specifically to self-heal disabled update components and may silently re-enable them on its own schedule.",
            TradeOff = "You will stop receiving security patches until re-enabled.",
            Grade = TweakGrade.B,
            Source = "Microsoft — wuauserv/BITS/UsoSvc service Start values",
            CustomApply = WindowsUpdateToggle.Apply,
            CustomRevert = WindowsUpdateToggle.Revert,
            InspectCommandState = WindowsUpdateToggle.IsDisabled,
        };

        public static IReadOnlyList<SystemTweak> All { get; } = new[] { WindowsUpdateOff };
    }
}
