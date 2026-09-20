using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace RionHub.Modules.Tools.Debloat;

/// <summary>Resolves installed package artwork without downloads or changing package access.</summary>
public static class StoreAppIconResolver
{
    // A 64px source remains crisp in the 24-DIP list at common display scales.
    public static string Resolve(string installLocation)
    {
        try
        {
            if (!Path.IsPathFullyQualified(installLocation) || installLocation.StartsWith(@"\\")) return "";
            string root = Path.GetFullPath(installLocation).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using var reader = XmlReader.Create(Path.Combine(root, "AppxManifest.xml"), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
            var manifest = XDocument.Load(reader);
            var visual = manifest.Descendants().Where(e => e.Name.LocalName == "VisualElements").ToList();
            foreach (var attribute in new[] { "Square44x44Logo", "Square30x30Logo", "Square150x150Logo", "Logo" })
                foreach (var element in visual)
                {
                    string logo = element.Attributes().FirstOrDefault(a => a.Name.LocalName == attribute)?.Value ?? "";
                    string path = ResolveAsset(root, logo, attribute == "Square150x150Logo" ? 150 : attribute == "Square30x30Logo" ? 30 : 44);
                    if (path.Length > 0) return path;
                }
            foreach (var logo in manifest.Descendants().Where(e => e.Name.LocalName == "Properties").Elements().Where(e => e.Name.LocalName == "Logo"))
            {
                string path = ResolveAsset(root, logo.Value, 50);
                if (path.Length > 0) return path;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException or NotSupportedException)
        { /* Missing/inaccessible package artwork falls back to the row's semantic icon. */ }
        return "";
    }

    private static string ResolveAsset(string root, string reference, int baseSize)
    {
        if (string.IsNullOrWhiteSpace(reference)) return "";
        try
        {
            if (reference.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase)) reference = reference[11..];
            if (Path.IsPathRooted(reference) || reference.Contains(':')) return "";
            string candidate = Path.GetFullPath(Path.Combine(root, reference.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return "";
            string extension = Path.GetExtension(candidate);
            if (!new[] { ".png", ".jpg", ".jpeg", ".bmp", ".ico" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) return "";
            string folder = Path.GetDirectoryName(candidate)!;
            if (!Directory.Exists(folder)) return "";
            string stem = Path.GetFileNameWithoutExtension(candidate);
            // Match the complete stem plus a qualifier delimiter, never an unrelated prefix.
            var assets = Directory.EnumerateFiles(folder).Where(path =>
                Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase) &&
                (Path.GetFileNameWithoutExtension(path).Equals(stem, StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileNameWithoutExtension(path).StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase)));
            return assets.OrderBy(path => ThemeRank(path))
                .ThenBy(path => SizeRank(path, baseSize)).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return ""; }
    }

    private static int ThemeRank(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        if (name.Contains("contrast-")) return 3;
        if (name.Contains("lightunplated") || name.Contains("theme-light")) return 2;
        return name.Contains("altform-unplated") || name.Contains("theme-dark") ? 0 : 1;
    }
    private static double SizeRank(string path, int baseSize)
    {
        string name = Path.GetFileName(path);
        var target = Regex.Match(name, @"targetsize-(\d+)", RegexOptions.IgnoreCase);
        var scale = Regex.Match(name, @"scale-(\d+)", RegexOptions.IgnoreCase);
        double size = target.Success && int.TryParse(target.Groups[1].Value, out int pixels) ? pixels
            : scale.Success && int.TryParse(scale.Groups[1].Value, out int percent) ? baseSize * percent / 100.0 : baseSize;
        return size >= 64 ? size - 64 : 10000 + 64 - size;
    }

    public static string FallbackGlyph(string identity) => identity.ToLowerInvariant() switch
    {
        "microsoft.windowscalculator" => "\uE8EF",
        "microsoft.windowscamera" => "\uE722",
        "microsoft.windows.photos" => "\uEB9F",
        "microsoft.screensketch" => "\uE8C6",
        "microsoft.windowsnotepad" or "microsoft.microsoftjournal" => "\uE70B",
        "microsoft.windowsterminal" or "microsoft.windows.devhome" => "\uE756",
        "microsoft.windowsstore" or "microsoft.storepurchaseapp" => "\uE719",
        "microsoft.gamingapp" or "microsoft.xboxgamingoverlay" or "microsoft.xboxapp" or "microsoft.microsoftsolitairecollection" => "\uE7FC",
        "microsoft.yourphone" => "\uE8EA",
        "microsoft.windowsalarms" => "\uE823",
        "microsoft.windowssoundrecorder" => "\uE720",
        "microsoft.zunemusic" => "\uE8D6",
        "microsoft.zunevideo" or "clipchamp.clipchamp" => "\uE714",
        "microsoft.paint" or "microsoft.mspaint" => "\uE790",
        "microsoft.bingweather" => "\uE706",
        "microsoft.bingnews" => "\uE8A5",
        "microsoft.windowsmaps" => "\uE707",
        "microsoft.outlookforwindows" or "microsoft.windowscommunicationsapps" => "\uE715",
        "msteams" or "microsoftteams" or "microsoft.skypeapp" => "\uE8F2",
        "microsoft.gethelp" or "microsoftcorporationii.quickassist" => "\uE897",
        "microsoft.people" or "microsoft.family" => "\uE716",
        _ => "\uE71D"
    };
}
