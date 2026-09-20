using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.Optimize;

namespace RadeonSoftwareSlimmer.Services
{
    public sealed record HistoricalDriverPackage(string Vendor, string Version, string Url, string Source);

    /// <summary>Exact release lookup against the detected product. No latest-version fallback.</summary>
    public sealed class HistoricalDriverInstaller
    {
        private static readonly SemaphoreSlim operation = new(1, 1);
        private readonly Func<string, CancellationToken, Task<string>> read;
        public HistoricalDriverInstaller(Func<string, CancellationToken, Task<string>> read = null) => this.read = read ?? ReadAsync;
        private static MatchCollection Matches(string text, string pattern) => Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(3));
        private static string Normalise(string s) => Regex.Replace(Regex.Replace(s.ToLowerInvariant(), @"\(tm\)|\(r\)|\bamd\b|\bnvidia\b|\bgraphics\b|\bseries\b", ""), "[^a-z0-9]", "");
        public async Task<HistoricalDriverPackage> ResolveAsync(DriverRecommendation selection, string gpu, bool win11, CancellationToken ct)
        {
            if (selection == null || !DriverRecommendationCatalog.ForVendor(selection.Vendor).Contains(selection) || string.IsNullOrWhiteSpace(gpu))
                throw new InvalidOperationException("Select a listed release and detect a supported GPU first.");
            if (selection.Vendor == "AMD")
            {
                string index = WebUtility.HtmlDecode(await read(DriverReleaseService.AmdIndex, ct));
                var pages = Matches(index, @"https://www\.amd\.com/en/support/downloads/drivers\.html/(?:graphics|processors)/[^""'\s<>]+?\.html")
                    .Cast<Match>().Select(m => m.Value).Distinct().Where(p => Normalise(Path.GetFileNameWithoutExtension(new Uri(p).LocalPath)) == Normalise(gpu)).ToArray();
                if (pages.Length != 1) throw new InvalidOperationException("The AMD GPU could not be matched exactly to an official product page.");
                foreach (string page in new[] { pages[0], pages[0].Replace("/drivers.html/", "/previous-drivers.html/") })
                {
                    string url = ParseAmd(await read(page, ct), selection.Version, win11);
                    if (url != null) return new("AMD", selection.Version, url, page);
                }
            }
            else
            {
                var xml = XDocument.Parse(await read("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3", ct));
                var products = xml.Descendants("LookupValue").Where(e => Normalise((string)e.Element("Name") ?? "") == Normalise(gpu)).ToArray();
                if (products.Length != 1 || !int.TryParse((string)products[0].Attribute("ParentID"), out int psid)
                    || !int.TryParse((string)products[0].Element("Value"), out int pfid) || psid <= 0 || pfid <= 0)
                    throw new InvalidOperationException("The NVIDIA GPU could not be matched exactly to an official product family.");
                var os = XDocument.Parse(await read("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=4", ct));
                var operatingSystems = os.Descendants("LookupValue").Where(e => win11
                    ? ((string)e.Element("Name") ?? "") is "Windows 11" or "Windows 11 64-bit"
                    : ((string)e.Element("Name") ?? "") == "Windows 10 64-bit").ToArray();
                if (operatingSystems.Length != 1 || !int.TryParse((string)operatingSystems[0].Element("Value"), out int osid))
                    throw new InvalidOperationException("The installed Windows version could not be matched exactly.");
                string page = $"https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid={psid}&pfid={pfid}&osID={osid}&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=100";
                string url = ParseNvidia(await read(page, ct), selection.Version);
                if (url != null) return new("NVIDIA", selection.Version, url, page);
            }
            throw new InvalidOperationException($"{selection.Version} is not available in the official listing for this GPU and Windows version. No different release was substituted.");
        }

        public static string ParseAmd(string html, string version, bool win11)
        {
            string section = WebUtility.HtmlDecode(html);
            var os = Matches(section, @"<button\b[^>]*>\s*Windows " + (win11 ? "11" : "10") + @"[^<]*</button>").Cast<Match>().FirstOrDefault();
            if (os == null) return null;
            section = section.Substring(os.Index + os.Length);
            var next = Matches(section, @"<h2\b[^>]*class=[""'][^""']*accordion-header").Cast<Match>().FirstOrDefault();
            if (next != null) section = section.Substring(0, next.Index);
            var urls = Matches(section, @"<article\b[^>]*>.*?</article>").Cast<Match>()
                .Where(m => Regex.IsMatch(m.Value, @"Adrenalin\s+" + Regex.Escape(version) + @"(?![\d.])", RegexOptions.IgnoreCase))
                .SelectMany(m => Matches(m.Value, @"https://drivers\.amd\.com/drivers/[^""'\s<>]+\.exe").Cast<Match>())
                .Select(m => m.Value).Where(u => IsPackageUrl("AMD", u, version)).Distinct().ToArray();
            if (urls.Length > 1) throw new InvalidDataException("Multiple AMD packages match; use the official page to choose the correct variant.");
            return urls.SingleOrDefault();
        }

        public static string ParseNvidia(string json, string version)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("IDS", out var ids) || ids.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Unrecognized NVIDIA driver catalog.");
            var urls = ids.EnumerateArray().Where(e => e.TryGetProperty("downloadInfo", out var d) && d.TryGetProperty("Version", out var v) && v.GetString() == version)
                .Select(e => e.GetProperty("downloadInfo")).SelectMany(d => d.EnumerateObject().Where(p => p.Name.Equals("DownloadURL", StringComparison.OrdinalIgnoreCase)).Select(p => p.Value.GetString()))
                .Where(u => IsPackageUrl("NVIDIA", u, version)).Distinct().ToArray();
            if (urls.Length > 1) throw new InvalidDataException("Multiple NVIDIA packages match; use the official page to choose the correct variant.");
            return urls.SingleOrDefault();
        }
        public static bool IsPackageUrl(string vendor, string url, string version) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
            && (vendor == "AMD" ? uri.Host == "drivers.amd.com" : vendor == "NVIDIA" && (uri.Host == "us.download.nvidia.com" || uri.Host == "international.download.nvidia.com"))
            && uri.LocalPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(Path.GetFileName(uri.LocalPath), @"(?<![\d.])" + Regex.Escape(version) + @"(?![\d.])")
            && !uri.LocalPath.Contains("minimalsetup", StringComparison.OrdinalIgnoreCase);

        private static HttpClient Client()
        {
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 RionDriverHistory/1.0");
            return client;
        }
        private static async Task<string> ReadAsync(string url, CancellationToken ct)
        {
            using var client = Client();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(35));
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400) throw new InvalidDataException("The official catalog redirected; automatic lookup stopped.");
            response.EnsureSuccessStatusCode();
            using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[65536]; int count;
            while ((count = await source.ReadAsync(chunk.AsMemory(), deadline.Token)) > 0)
            {
                if (buffer.Length + count > 8 * 1024 * 1024) throw new InvalidDataException("The vendor catalog exceeds the metadata size limit.");
                buffer.Write(chunk, 0, count);
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }

        public static bool Trusted(string vendor, DriverInstallerInfo info) => info != null
            && (info.SignatureStatus == SignatureStatus.Valid || info.SignatureStatus == SignatureStatus.ValidButNotAmd)
            && (vendor == "AMD" ? InstallerValidator.IsAmdPublisher(info.Publisher) : vendor == "NVIDIA" && info.Publisher == "NVIDIA Corporation");

        public async Task DownloadAndLaunchAsync(DriverRecommendation selection, string gpu, IProgress<string> progress, CancellationToken ct, Action onLaunching = null)
        {
            if (!await operation.WaitAsync(0, ct)) throw new InvalidOperationException("Another historical driver operation is already running.");
            try
            {
                if (!Environment.Is64BitOperatingSystem || Environment.OSVersion.Version.Major != 10) throw new InvalidOperationException("Automatic download requires Windows 10 or 11, 64-bit.");
                progress.Report("Finding " + selection.Version + " for " + gpu + "…");
                var package = await ResolveAsync(selection, gpu, Environment.OSVersion.Version.Build >= 22000, ct);
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RionWin11", "downloads", "History", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, Path.GetFileName(new Uri(package.Url).LocalPath));
                try
                {
                    using var client = Client();
                    using var request = new HttpRequestMessage(HttpMethod.Get, package.Url);
                    if (package.Vendor == "AMD") request.Headers.Referrer = new Uri(package.Source);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400) throw new InvalidDataException("The package URL redirected; automatic download stopped.");
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentType?.MediaType?.StartsWith("text/") == true) throw new InvalidDataException("The vendor returned a web page instead of an installer.");
                    using (var source = await response.Content.ReadAsStreamAsync(ct))
                    using (var target = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                    {
                        byte[] buffer = new byte[65536]; long total = 0; int count; long lastMb = -1;
                        while ((count = await source.ReadAsync(buffer.AsMemory(), ct)) > 0)
                        {
                            total += count;
                            if (total > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("Installer exceeds the download size limit.");
                            await target.WriteAsync(buffer.AsMemory(0, count), ct);
                            long mb = total / 1024 / 1024;
                            if (mb != lastMb) { lastMb = mb; progress.Report($"Downloading {package.Version} · {mb} MB"); }
                        }
                        if (total < 2 * 1024 * 1024 || response.Content.Headers.ContentLength is long expected && total != expected) throw new InvalidDataException("The installer download is incomplete.");
                    }
                    ct.ThrowIfCancellationRequested();
                    progress.Report("Verifying " + package.Version + " signature…");
                    // Deny replacement/write/delete from validation through process exit.
                    using var fileLock = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var info = await Task.Run(() => new InstallerValidator().Inspect(file, package.Url, DateTime.Now), ct);
                    if (!Trusted(package.Vendor, info)) throw new InvalidDataException("Installer signature or publisher verification failed. Setup was not launched.");
                    ct.ThrowIfCancellationRequested();
                    onLaunching?.Invoke();
                    progress.Report("Opening " + package.Vendor + " " + package.Version + " setup. Complete the vendor's installation prompts; the display may flicker. Cancellation is now handled by the vendor installer.");
                    var result = await ShellRunner.RunAsync(file, "", null, timeoutMs: 0);
                    if (result.ExitCode != 0 && result.ExitCode != 3010) throw new InvalidOperationException("Vendor installer exited with code " + result.ExitCode + ". " + result.Output);
                    progress.Report("Vendor setup exited" + (result.ExitCode == 3010 ? " · restart required" : "") + ". Check the installed driver version after setup/restart; installation success was not independently verified.");
                }
                catch { try { File.Delete(file); } catch { } throw; }
            }
            finally { operation.Release(); }
        }
    }
}
