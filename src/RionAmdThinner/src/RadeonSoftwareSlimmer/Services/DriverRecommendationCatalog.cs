using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RadeonSoftwareSlimmer.Services
{
    public sealed record DriverRecommendation(string Vendor, string Version, DateTime Released,
        string UseCase, string Evidence, string Caveat, string OfficialUrl, string EvidenceUrl, bool IsOptimal = false)
    {
        public string Label => Version + (IsOptimal ? " · Optimal" : "") + " · " + Released.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        // The shared ComboBox template uses SelectionBoxItem directly.
        public override string ToString() => Label;
    }

    // Editorial evidence snapshot. A live metadata refresh must never advance this date.
    public static class DriverRecommendationCatalog
    {
        public static DateTime ReviewedOn => new(2026, 9, 8);
        private static readonly IReadOnlyList<DriverRecommendation> entries = Array.AsReadOnly(new[]
        {
            new DriverRecommendation("AMD", "26.8.1", new(2026, 8, 20),
                "Optimal · general gaming baseline for supported Radeon GPUs",
                "Rion's general-use pick from AMD's WHQL Recommended branch. Optimal describes this starting recommendation; performance has not been measured on your PC.",
                "RX 9000 users playing Starfield or Apex Legends should review 26.9.1, which lists crash fixes. Use OEM guidance for notebooks, hybrid graphics and handhelds. Keep a working driver unless you need a fix or update.",
                "https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-8-1.html",
                "https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-8-1.html", true),
            new DriverRecommendation("AMD", "26.9.1", new(2026, 9, 3),
                "RX 9000 · Starfield and Apex Legends crash fixes",
                "AMD's Optional release lists fixes for intermittent crashes in these games on RX 9000 graphics.",
                "Choose this for a matching issue or newly supported game. Optional is a release channel, not proof that every system will improve.",
                "https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-9-1.html",
                "https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-9-1.html"),
            new DriverRecommendation("AMD", "26.6.4", new(2026, 6, 29),
                "RX 7900 XTX · reported rollback candidate",
                "The 26.8.1 community discussion includes a September 2 RX 7900 XTX report favoring rollback to 26.6.4 after freezes. This is anecdotal evidence.",
                "26.6.4 has its own known issues, including Blender/Cinema 4D problems on RX 7000 and newer. Consider only for a matching regression.",
                "https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-6-4.html",
                "https://www.reddit.com/r/Amd/comments/1vtixov/amd_software_adrenalin_edition_2681_driver/"),
            new DriverRecommendation("AMD", "26.3.1", new(2026, 3, 19),
                "RX 7000 and newer · historical Blender / Cinema 4D workaround",
                "AMD's 26.6.4 release notes explicitly recommend 26.3.1 for affected model flickering, rendering failures and Blender crashes.",
                "A documented workaround for those issues, not a current general gaming recommendation. Check whether a newer release fixes your issue first.",
                "https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-3-1.html",
                "https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-6-4.html"),
            new DriverRecommendation("NVIDIA", "616.64", new(2026, 9, 3),
                "Optimal · general gaming baseline for supported GeForce GPUs",
                "Rion's general-use pick is NVIDIA's September 3 Game Ready WHQL release. Optimal is a starting recommendation, not a benchmark result or a guarantee for every GPU.",
                "If virtual displays or browser flickering are affected, review NVIDIA's 616.86 hotfix through the evidence link. Hotfixes have a shorter QA cycle. For notebooks and specific regressions, check OEM guidance before replacing a working driver.",
                "https://www.nvidia.com/en-us/drivers/details/278445/",
                "https://nvidia.custhelp.com/app/answers/detail/a_id/5906", true),
            new DriverRecommendation("NVIDIA", "616.56", new(2026, 8, 26),
                "RTX 40 series · Chief Architect X17 fix",
                "Chief Architect QA confirmed the fix in 616.56 on August 26. A user reported success with Game Ready while still reporting problems with Studio.",
                "Game Ready entry; do not assume identical application results with Studio. This is application-specific evidence, not proof of overall stability.",
                "https://www.nvidia.com/Download/driverResults.aspx/278153/en-us/",
                "https://chieftalk.chiefarchitect.com/topic/49060-update-version-61656-is-now-working-dont-update-to-nvidia-61088-driver-as-chief-architect-x17-unexpectedly-quit/"),
            new DriverRecommendation("NVIDIA", "591.86", new(2026, 1, 27),
                "Affected HP Omen notebooks · idle GPU activity rollback",
                "Recent HP Omen community reports retain 591.86 because newer drivers reportedly keep the notebook GPU active at idle. This has not been verified on this PC.",
                "Notebook-specific anecdote, not a desktop recommendation. Confirm OEM guidance and exact GPU support; newer GPU models may require a newer package.",
                "https://www.nvidia.com/download/driverResults.aspx/263200/en-us/",
                "https://www.reddit.com/r/HPOmen/comments/1vznyod/where_can_we_go_to_complain_about_the_nvidia/")
        });

        public static IReadOnlyList<DriverRecommendation> ForVendor(string vendor) =>
            Array.AsReadOnly(entries.Where(e => e.Vendor == vendor).ToArray());

        public static bool IsEvidenceUrl(string vendor, string url) =>
            entries.Any(e => e.Vendor == vendor && e.EvidenceUrl == url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo);
    }
}
