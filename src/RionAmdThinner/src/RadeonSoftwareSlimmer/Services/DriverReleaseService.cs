using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RadeonSoftwareSlimmer.Services
{
    public sealed record DriverRelease(string Version, DateTime ReleaseDate, string Channel, string Certification,
        string Size, string NotesUrl);
    public sealed record DriverReleaseSnapshot(string Vendor, string Scope, string SourceUrl,
        DateTimeOffset CheckedAt, DriverRelease Latest, DriverRelease Alternative);

    /// <summary>Read-only vendor metadata. Never downloads or executes an installer.</summary>
    public sealed class DriverReleaseService
    {
        public const string AmdIndex = "https://www.amd.com/en/support/download/drivers.html";
        public const string AmdReference = "https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-9000-series/amd-radeon-rx-9070-xt.html";
        public const string NvidiaIndex = "https://www.nvidia.com/Download/processFind.aspx?dtcid=1&lid=1&osid=135";
        private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
        private readonly Func<Uri, CancellationToken, Task<string>> fetch;
        public DriverReleaseService(Func<Uri, CancellationToken, Task<string>> fetch = null) { this.fetch = fetch ?? FetchAsync; }

        public static bool IsOfficial(string vendor, Uri uri) => vendor is "AMD" or "NVIDIA" && uri != null && uri.IsAbsoluteUri
            && uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
            && uri.Host.Equals(vendor == "AMD" ? "www.amd.com" : "www.nvidia.com", StringComparison.OrdinalIgnoreCase);

        private static async Task<string> FetchAsync(Uri uri, CancellationToken ct)
        {
            string vendor = uri.Host == "www.amd.com" ? "AMD" : "NVIDIA";
            for (int redirects = 0; redirects <= 3; redirects++)
            {
                if (!IsOfficial(vendor, uri)) throw new InvalidDataException("The release source is not an official vendor HTTPS page.");
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) RionDriverRelease/1.0");
                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400 && response.Headers.Location != null)
                { uri = new Uri(uri, response.Headers.Location); continue; }
                response.EnsureSuccessStatusCode();
                const int limit = 4 * 1024 * 1024;
                if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Release page exceeds the metadata size limit.");
                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                byte[] chunk = new byte[16384];
                int count;
                while ((count = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + count > limit) throw new InvalidDataException("Release page exceeds the metadata size limit.");
                    buffer.Write(chunk, 0, count);
                }
                return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
            }
            throw new InvalidDataException("The vendor redirected the release lookup too many times.");
        }

        private static MatchCollection Matches(string text, string pattern) => Regex.Matches(text, pattern,
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        private static Match Match(string text, string pattern) => Regex.Match(text, pattern,
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        private static string Plain(string html) => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ",
            RegexOptions.None, TimeSpan.FromSeconds(2))), @"\s+", " ").Trim();
        private static string Normalise(string name) => Regex.Replace(
            Regex.Replace(name.ToLowerInvariant(), @"\(tm\)|\(r\)|\bamd\b|\bnvidia\b|\bgraphics\b|\bseries\b", " "), @"[^a-z0-9]+", " ").Trim();

        public async Task<DriverReleaseSnapshot> ReadAsync(string vendor, string gpuName, CancellationToken ct)
        {
            if (vendor is not ("AMD" or "NVIDIA")) throw new ArgumentException("Unknown driver vendor.");
            string url = vendor == "AMD" ? AmdReference : NvidiaIndex;
            string scope = vendor == "AMD" ? "Radeon RX 9070 XT reference listing · Windows 11" : "Public GeForce listing · Windows 11";
            if (!string.IsNullOrWhiteSpace(gpuName))
            {
                if (vendor == "AMD")
                {
                    string index = WebUtility.HtmlDecode(await fetch(new Uri(AmdIndex), ct).ConfigureAwait(false));
                    var links = Matches(index, @"https://www\.amd\.com/en/support/downloads/drivers\.html/(?:graphics|processors)/[^""'\s<>]+?\.html")
                        .Select(m => m.Value).Distinct().Where(link => Normalise(Path.GetFileNameWithoutExtension(new Uri(link).AbsolutePath)) == Normalise(gpuName)).ToList();
                    if (links.Count == 1) { url = links[0]; scope = gpuName + " · Windows 11 product listing"; }
                    else scope += " · detected GPU not matched";
                }
                else
                {
                    var xml = await fetch(new Uri("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3"), ct).ConfigureAwait(false);
                    var candidates = XDocument.Parse(xml).Descendants("LookupValue")
                        .Where(e => Normalise((string)e.Element("Name") ?? "") == Normalise(gpuName)).ToList();
                    if (candidates.Count == 1 && int.TryParse((string)candidates[0].Attribute("ParentID"), out int series)
                        && int.TryParse((string)candidates[0].Element("Value"), out int product) && series > 0 && product > 0)
                    { url += $"&psid={series}&pfid={product}"; scope = gpuName + " · Windows 11 product listing"; }
                    else scope += " · detected GPU not matched";
                }
            }
            string html = await fetch(new Uri(url), ct).ConfigureAwait(false);
            var releases = vendor == "AMD" ? ParseAmd(html, url) : ParseNvidia(html);
            var latest = releases.OrderByDescending(r => r.ReleaseDate).ThenByDescending(r => Version.Parse(r.Version)).FirstOrDefault();
            if (vendor == "NVIDIA") latest = releases.Where(r => r.Channel == "Game Ready").OrderByDescending(r => r.ReleaseDate).ThenByDescending(r => Version.Parse(r.Version)).FirstOrDefault();
            if (latest == null) throw new InvalidDataException("The vendor page did not contain a recognized release. Open the official page or retry later.");
            var alternative = releases.Where(r => vendor == "AMD" ? r.Channel == "Recommended" && r.Version != latest.Version : r.Channel == "Studio")
                .OrderByDescending(r => r.ReleaseDate).ThenByDescending(r => Version.Parse(r.Version)).FirstOrDefault();
            return new(vendor, scope, url, DateTimeOffset.UtcNow, latest, alternative);
        }

        public static IReadOnlyList<DriverRelease> ParseAmd(string html, string sourceUrl)
        {
            var source = new Uri(sourceUrl);
            if (!IsOfficial("AMD", source)) throw new InvalidDataException("Unexpected AMD source.");
            // Restrict records to the Windows 11 accordion, never combine dates from another OS or package.
            var os = Match(html, @"<button\b[^>]*>\s*Windows 11[^<]*</button>");
            if (!os.Success) throw new InvalidDataException("Windows 11 driver section was not found.");
            string section = html.Substring(os.Index + os.Length);
            var next = Match(section, @"<h2\b[^>]*class=[""'][^""']*accordion-header");
            if (next.Success) section = section.Substring(0, next.Index);
            var found = new List<DriverRelease>();
            foreach (Match article in Matches(section, @"<article\b[^>]*>.*?</article>"))
            {
                string body = article.Value;
                var revision = Match(body, @"Revision Number</strong>\s*<p>\s*Adrenalin\s+(\d{2}\.\d{1,2}\.\d{1,2})([^<]*)</p>");
                var date = Match(body, @"Release Date</strong>\s*<p>\s*(\d{4}-\d{2}-\d{2})\s*</p>");
                if (!revision.Success || !DateTime.TryParseExact(date.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var released)) continue;
                string tag = Plain(revision.Groups[2].Value);
                string channel = tag.Contains("Optional", StringComparison.OrdinalIgnoreCase) ? "Optional" : tag.Contains("Recommended", StringComparison.OrdinalIgnoreCase) ? "Recommended" : "Not specified";
                var note = Match(body, @"href=[""']([^""']*/release-notes/[^""']+)[""']");
                string notes = note.Success ? new Uri(source, WebUtility.HtmlDecode(note.Groups[1].Value)).AbsoluteUri : sourceUrl;
                if (!IsOfficial("AMD", new Uri(notes))) throw new InvalidDataException("Unexpected AMD release-notes link.");
                string size = Plain(Match(body, @"File Size</strong>\s*<p>(.*?)</p>").Groups[1].Value);
                found.Add(new(revision.Groups[1].Value, released, channel, tag.Contains("WHQL", StringComparison.OrdinalIgnoreCase) ? "WHQL · vendor listed" : "Certification not listed", size, notes));
            }
            return found.GroupBy(r => (r.Version, r.Channel)).Select(g => g.First()).ToList();
        }

        public static IReadOnlyList<DriverRelease> ParseNvidia(string html)
        {
            var found = new List<DriverRelease>();
            foreach (Match row in Matches(html, @"<tr\s+id=[""']driverList[""'][^>]*>.*?</tr>"))
            {
                var cells = Matches(row.Value, @"<td\b[^>]*>(.*?)</td>").Select(m => m.Groups[1].Value).ToList();
                if (cells.Count < 4) continue;
                string name = Plain(cells[1]);
                string channel = name.StartsWith("GeForce Game Ready Driver", StringComparison.OrdinalIgnoreCase) ? "Game Ready"
                    : name.StartsWith("NVIDIA Studio Driver", StringComparison.OrdinalIgnoreCase) ? "Studio" : null;
                string version = Plain(cells[2]);
                if (channel == null || !Match(version, @"^\d{3}\.\d{2}$").Success
                    || !DateTime.TryParseExact(Plain(cells[3]), "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
                var link = Match(cells[1], @"href=[""']([^""']+)[""']");
                if (!link.Success) continue;
                var notes = new Uri(new Uri("https://www.nvidia.com"), WebUtility.HtmlDecode(link.Groups[1].Value));
                if (!IsOfficial("NVIDIA", notes)) throw new InvalidDataException("Unexpected NVIDIA release link.");
                found.Add(new(version, date, channel, name.Contains("WHQL", StringComparison.OrdinalIgnoreCase) ? "WHQL · vendor listed" : "Certification not listed", "", notes.AbsoluteUri));
            }
            return found.GroupBy(r => (r.Version, r.Channel)).Select(g => g.First()).ToList();
        }
    }
}
