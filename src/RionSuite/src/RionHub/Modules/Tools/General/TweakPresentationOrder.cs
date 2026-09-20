using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Modules.Tools.General;

/// <summary>Presentation only; batch eligibility and original categories are unchanged.</summary>
public static class TweakPresentationOrder
{
    public const string AdvancedSection = "Advanced controls";
    public static bool IsAdvanced(TweakBundle bundle) => bundle.IsAdjustable || bundle.ApplyBlocked
        || bundle.Members.Any(t => t.Id == "priority-separation" || t.Id.StartsWith("mmcss-", StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<TweakBundle> Sort(IEnumerable<TweakBundle> bundles, bool allTweaks) => bundles
        .OrderBy(b => allTweaks && IsAdvanced(b) ? 1 : 0)
        .ThenBy(b => Math.Max(0, TweakBundleCatalog.Sections.ToList().IndexOf(b.Section)));
}
