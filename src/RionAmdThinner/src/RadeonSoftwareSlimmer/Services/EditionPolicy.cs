namespace RadeonSoftwareSlimmer.Services;

/// <summary>Build-time product boundaries, shared by navigation and driver workflows.</summary>
public static class EditionPolicy
{
#if RION_FREE
    public static bool IsFree => true;
#else
    public static bool IsFree => false;
#endif
    public static bool HasAdvancedTools => !IsFree;
    public static string Title => IsFree ? "Rion Win 11 · Free version" : "Rion Win 11 · Paid";
    public static bool IncludesTweak(string id, string section) => !IsFree
        || section == "MMCSS" || id == "bundle-game-capture" || id == "bundle-priority-separation";
}
