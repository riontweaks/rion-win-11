using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NvidiaDriverTool.Services
{
    /// <summary>
    /// Resolves NVIDIA driver installers from NVIDIA's own lookup service. Two endpoints:
    ///   - <c>lookupValueSearch.aspx?TypeID=3</c> — XML product-family catalog,
    ///     <c>&lt;LookupValue ParentID="{series}"&gt;&lt;Name&gt;GeForce RTX 4070&lt;/Name&gt;&lt;Value&gt;{family}&lt;/Value&gt;&lt;/LookupValue&gt;</c>.
    ///     <c>TypeID=4</c> is the same shape for operating systems.
    ///   - <c>AjaxDriverService.php?func=DriverManualLookup&amp;psid=&amp;pfid=&amp;osID=&amp;languageCode=1033&amp;isWHQL=1&amp;dch=1</c>
    ///     — JSON, version at <c>IDS[0].downloadInfo.Version</c>. The download-URL field is read
    ///     defensively across candidate names and throws naming what it saw rather than guessing.
    ///
    /// The download itself uses <see cref="RadeonSoftwareSlimmer.Services.DriverDownloadService.DownloadAsync"/>.
    /// </summary>
    public sealed class NvidiaDriverDownloadService
    {
        private const string LookupBase = "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=";
        private const string AjaxDriverServiceUrl =
            "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php";

        private readonly HttpClient _http;

        public NvidiaDriverDownloadService()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(20);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RionNvidiaDriverTool");
        }

        public sealed class LatestDriverInfo
        {
            public string Version { get; set; }
            public string DownloadUrl { get; set; }
        }

        /// <summary>Product-family ("pfid") and product-series ("psid") ids for a detected GPU
        /// name, resolved from NVIDIA's own lookup catalog rather than hardcoded per model.</summary>
        public async Task<(int Psid, int Pfid)> ResolveProductIdsAsync(string gpuName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(gpuName))
                throw new InvalidOperationException("No NVIDIA GPU was detected on this PC.");

            string wanted = Normalise(gpuName);
            string xml = await _http.GetStringAsync(LookupBase + "3").ConfigureAwait(false);
            XDocument doc = XDocument.Parse(xml);

            var candidates = doc.Descendants("LookupValue")
                .Select(e => new
                {
                    Name = (string)e.Element("Name"),
                    Value = (string)e.Element("Value"),
                    ParentId = (string)e.Attribute("ParentID"),
                })
                .Where(c => c.Name != null && c.Value != null && c.ParentId != null)
                .Select(c => new { c.Name, Pfid = int.Parse(c.Value), Psid = int.Parse(c.ParentId), Score = MatchScore(Normalise(c.Name), wanted) })
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .ToList();

            if (candidates.Count == 0)
                throw new InvalidOperationException($"Couldn't match “{gpuName}” to an NVIDIA product family.");

            if (candidates[0].Score < 100 || candidates.Count(c => c.Score == candidates[0].Score) > 1)
                throw new InvalidOperationException("GPU match is not exact and unique. Download the correct package from NVIDIA and use Browse.");

            return (candidates[0].Psid, candidates[0].Pfid);
        }

        /// <summary>NVIDIA's numeric id for the running Windows version, resolved from the OS
        /// catalog (TypeID=4) rather than a hardcoded guess.</summary>
        private async Task<int> ResolveWindowsOsIdAsync(CancellationToken ct)
        {
            bool win11 = Environment.OSVersion.Version.Build >= 22000;
            string xml = await _http.GetStringAsync(LookupBase + "4").ConfigureAwait(false);
            XDocument doc = XDocument.Parse(xml);

            var osEntries = doc.Descendants("LookupValue")
                .Select(e => new { Name = (string)e.Element("Name"), Value = (string)e.Element("Value") })
                .Where(e => e.Name != null && e.Value != null && e.Name.IndexOf("64-bit", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            string wantedLabel = win11 ? "Windows 11" : "Windows 10";
            var match = osEntries.FirstOrDefault(e => e.Name.IndexOf(wantedLabel, StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? osEntries.FirstOrDefault(e => e.Name.IndexOf("Windows 10", StringComparison.OrdinalIgnoreCase) >= 0);

            if (match == null)
                throw new InvalidOperationException("Couldn't resolve a Windows entry in NVIDIA's OS catalog.");

            return int.Parse(match.Value);
        }

        /// <summary>Resolves the current version + download URL for <paramref name="gpuName"/>'s
        /// latest driver. Throws on failure — same "no silent wrong answer" stance as the AMD side.</summary>
        public async Task<LatestDriverInfo> ResolveLatestDriverAsync(string gpuName, CancellationToken ct = default)
        {
            (int psid, int pfid) = await ResolveProductIdsAsync(gpuName, ct).ConfigureAwait(false);
            int osId = await ResolveWindowsOsIdAsync(ct).ConfigureAwait(false);

            string url = AjaxDriverServiceUrl +
                $"?func=DriverManualLookup&psid={psid}&pfid={pfid}&osID={osId}" +
                "&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1";

            string json = await _http.GetStringAsync(url).ConfigureAwait(false);

            // Defensive field read: only .Version is confirmed against a real response; the
            // download-URL field name is a best-effort guess across the common candidates rather
            // than a single hardcoded (and possibly wrong) name.
            Match versionMatch = Regex.Match(json, "\"Version\"\\s*:\\s*\"([^\"]+)\"");
            if (!versionMatch.Success)
                throw new InvalidOperationException(
                    "NVIDIA's driver lookup returned a response this tool doesn't recognise yet — " +
                    "the field names may have changed. Raw response starts: " +
                    json.Substring(0, Math.Min(200, json.Length)));

            Match urlMatch = Regex.Match(json,
                "\"(DownloadURL|DownloadUrl|downloadUrl|downloadURL)\"\\s*:\\s*\"([^\"]+)\"");
            if (!urlMatch.Success)
                throw new InvalidOperationException(
                    "Found a driver version (" + versionMatch.Groups[1].Value + ") but couldn't find a " +
                    "download URL field in NVIDIA's response — needs a look at the real JSON shape to fix the field name.");

            string downloadUrl = urlMatch.Groups[2].Value.Replace("\\/", "/");
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri)
                || downloadUri.Scheme != "https" || !downloadUri.Host.EndsWith(".nvidia.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NVIDIA lookup did not return an HTTPS NVIDIA download URL.");
            return new LatestDriverInfo
            {
                Version = versionMatch.Groups[1].Value,
                DownloadUrl = downloadUrl,
            };
        }

        /// <summary>Thin pass-through to the fully generic AMD download implementation — no
        /// NVIDIA-specific logic needed here (no referer gate, unlike drivers.amd.com).</summary>
        public Task<string> DownloadAsync(string url, string targetFolder, IProgress<double> progress, CancellationToken ct = default) =>
            new RadeonSoftwareSlimmer.Services.DriverDownloadService().DownloadAsync(url, targetFolder, progress, referer: null, ct: ct);

        private static string Normalise(string name)
        {
            string s = name.ToLowerInvariant();
            s = Regex.Replace(s, @"\(tm\)|\(r\)|\bnvidia\b|\bgraphics\b", " ");
            s = Regex.Replace(s, @"[^a-z0-9]+", " ").Trim();
            return s;
        }

        private static int MatchScore(string candidate, string wanted)
        {
            if (string.Equals(candidate, wanted, StringComparison.OrdinalIgnoreCase)) return 100;
            if (candidate.Contains(wanted) || wanted.Contains(candidate)) return 60;

            var a = candidate.Split(' ');
            var b = wanted.Split(' ');
            int overlap = a.Count(t => b.Contains(t));
            return overlap >= 2 ? 20 + overlap : 0;
        }
    }
}
