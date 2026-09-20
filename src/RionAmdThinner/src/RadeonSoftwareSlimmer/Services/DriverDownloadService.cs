using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Resolves and downloads AMD driver installers straight from amd.com. Two flavours:
    ///   - the small "auto-detect" / minimal web installer (a bootstrapper, nothing to slim), and
    ///   - the full "AMD Software: Adrenalin Edition" package for a GPU, which is what the
    ///     slimmer actually operates on.
    /// Nothing is hard-coded except AMD's URL <em>structure</em>: the app fetches AMD's own
    /// product index, matches your GPU name to its product-page slug, and scrapes the current
    /// installer link off that page at run time.
    /// </summary>
    public sealed class DriverDownloadService
    {
        /// <summary>
        /// The page every drivers.amd.com link is scraped from. Also required as the
        /// <c>Referer</c> header on the direct download — AMD's CDN 302-redirects to an HTML
        /// "Download Incomplete" page for both the minimal and full installers when it's absent.
        /// </summary>
        public const string DriversPage = "https://www.amd.com/en/support/download/drivers.html";
        private const string ProductPageBase = "https://www.amd.com/en/support/downloads/drivers.html";

        // small bootstrapper, e.g. .../installer/26.10/whql/amd-software-adrenalin-edition-26.8.1-minimalsetup-260818_web.exe
        private static readonly Regex MinimalInstallerUrl = new Regex(
            @"https://drivers\.amd\.com/drivers/installer/[^""'\s]+?minimalsetup[^""'\s]*?_web\.exe",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // full package, e.g. https://drivers.amd.com/drivers/whql-amd-software-adrenalin-edition-26.8.1-win11-b.exe
        private static readonly Regex FullPackageUrl = new Regex(
            @"https://drivers\.amd\.com/drivers/(?!installer/)[^""'\s]*?adrenalin-edition-[^""'\s]*?\.exe",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // product-page slugs listed on the main drivers page, e.g. /graphics/radeon-rx/radeon-rx-7000-series/amd-radeon-rx-7700-xt
        private static readonly Regex ProductSlug = new Regex(
            @"/graphics/[a-z0-9-]+/[a-z0-9-]+/[a-z0-9-]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly HttpClient _http;

        public DriverDownloadService()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(20);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RionAmdThinner");
        }

        public sealed class SeriesPackage
        {
            public string InstallerUrl { get; set; }
            public string ProductPageUrl { get; set; }
        }

        /// <summary>Scrapes amd.com for the current auto-detect (minimal / web) installer URL. Throws on failure.</summary>
        public async Task<string> ResolveLatestInstallerUrlAsync(CancellationToken ct = default)
        {
            StaticViewModel.AddLogMessage("Looking up the latest AMD auto-detect installer...");
            string html = await _http.GetStringAsync(DriversPage).ConfigureAwait(false);

            Match m = MinimalInstallerUrl.Match(html);
            if (!m.Success)
                throw new InvalidOperationException(
                    "Could not find the auto-detect installer link on amd.com. AMD may have changed their page — use Browse instead.");

            StaticViewModel.AddLogMessage("Found " + m.Value);
            return m.Value;
        }

        /// <summary>
        /// Finds the AMD product page for <paramref name="gpuName"/> (e.g. "AMD Radeon RX 7700 XT")
        /// and scrapes the current full "Adrenalin Edition" package link off it.
        /// Returns both the installer URL and the product-page URL (so callers can fall back to
        /// just opening the page). Throws if nothing matches.
        /// </summary>
        public async Task<SeriesPackage> ResolveGpuSeriesPackageAsync(string gpuName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(gpuName))
                throw new InvalidOperationException("No AMD GPU was detected on this PC.");

            string wanted = NormaliseModel(gpuName);   // "AMD Radeon RX 7700 XT" -> "radeon-rx-7700-xt"
            StaticViewModel.AddLogMessage($"Finding the AMD product page for “{gpuName}”...");

            string index = await _http.GetStringAsync(DriversPage).ConfigureAwait(false);
            string slug = ProductSlug.Matches(index)
                .Select(x => x.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(s => MatchScore(s, wanted))
                .FirstOrDefault();

            if (slug == null || MatchScore(slug, wanted) != 100)
                throw new InvalidOperationException(
                    $"Couldn't match “{gpuName}” to an AMD product page. Use the auto-detect installer or Browse.");

            string productPageUrl = ProductPageBase + slug + ".html";
            StaticViewModel.AddLogMessage("Product page: " + productPageUrl);

            string page = await _http.GetStringAsync(productPageUrl).ConfigureAwait(false);
            string installer = FullPackageUrl.Matches(page)
                .Select(x => x.Value)
                .Where(u => u.IndexOf("minimalsetup", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderByDescending(PreferOsScore)
                .FirstOrDefault();

            if (installer == null)
                throw new InvalidOperationException(
                    "Found the product page but not a full-package link on it. Opening the page instead.");

            StaticViewModel.AddLogMessage("Found full package: " + installer);
            return new SeriesPackage { InstallerUrl = installer, ProductPageUrl = productPageUrl };
        }

        private static string NormaliseModel(string gpuName)
        {
            string s = gpuName.ToLowerInvariant();
            s = Regex.Replace(s, @"\(tm\)|\(r\)|\bamd\b|\bgraphics\b|\bseries\b", " ");
            s = Regex.Replace(s, @"[^a-z0-9]+", "-").Trim('-');
            return s;   // e.g. "radeon-rx-7700-xt"
        }

        /// <summary>Higher = better. Exact slug tail match beats a partial one.</summary>
        private static int MatchScore(string slug, string wanted)
        {
            string tail = slug.Substring(slug.LastIndexOf('/') + 1);   // amd-radeon-rx-7700-xt
            tail = tail.StartsWith("amd-", StringComparison.OrdinalIgnoreCase) ? tail.Substring(4) : tail;
            if (string.Equals(tail, wanted, StringComparison.OrdinalIgnoreCase)) return 100;
            if (tail.Contains(wanted) || wanted.Contains(tail)) return 60;

            // token overlap
            var a = tail.Split('-');
            var b = wanted.Split('-');
            int overlap = a.Count(t => b.Contains(t));
            return overlap >= 3 ? 20 + overlap : 0;
        }

        private static int PreferOsScore(string url)
        {
            bool win11 = Environment.OSVersion.Version.Build >= 22000;
            if (win11 && url.IndexOf("win11", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (!win11 && url.IndexOf("win10", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            return 1;
        }

        /// <summary>
        /// Downloads <paramref name="url"/> into <paramref name="targetFolder"/> and returns the
        /// full local path. <paramref name="progress"/> receives 0..1 (or -1 when total is unknown).
        /// <paramref name="referer"/> is required for drivers.amd.com links — without it AMD
        /// redirects to a "download incomplete" HTML page.
        /// </summary>
        public async Task<string> DownloadAsync(string url, string targetFolder,
            IProgress<double> progress, string referer = null, CancellationToken ct = default)
        {
            Directory.CreateDirectory(targetFolder);

            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "amd-software-adrenalin-edition-autodetect.exe";
            string destination = Path.Combine(targetFolder, fileName);

            StaticViewModel.AddLogMessage("Downloading " + fileName + " ...");

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream,*/*");
                if (!string.IsNullOrWhiteSpace(referer))
                    request.Headers.TryAddWithoutValidation("Referer", referer);

                using (HttpResponseMessage response = await _http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    // AMD's gate: no valid referer -> a small text/html "Download-Incomplete" page.
                    string mediaType = response.Content.Headers.ContentType?.MediaType;
                    if (mediaType != null && mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "AMD returned a web page instead of the installer (the direct link is referer-gated). " +
                            "Try again, or use the auto-detect installer / Browse.");

                    long? total = response.Content.Headers.ContentLength;
                    using (Stream src = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
                    {
                        byte[] buffer = new byte[1 << 16];
                        long read = 0;
                        int n;
                        while ((n = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await dst.WriteAsync(buffer, 0, n, ct).ConfigureAwait(false);
                            read += n;
                            progress?.Report(total.HasValue && total.Value > 0 ? (double)read / total.Value : -1);
                        }
                    }
                }
            }

            // A real Adrenalin package is hundreds of MB; the minimal web installer is ~10 MB.
            // Anything under ~2 MB means we got an error page, not a driver.
            long size = new FileInfo(destination).Length;
            if (size < 2L * 1024 * 1024)
            {
                try { File.Delete(destination); } catch { /* best effort */ }
                throw new InvalidOperationException(
                    $"The download was only {size / 1024} KB — AMD served an error page, not the installer. " +
                    "Use the auto-detect installer or download the package manually.");
            }

            StaticViewModel.AddLogMessage($"Downloaded {size / 1024 / 1024} MB to {destination}");
            return destination;
        }
    }
}
