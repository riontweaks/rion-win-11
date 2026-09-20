using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>Downloads tools atomically and validates Authenticode before launching.</summary>
    public static class PortableToolDownloader
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        /// <summary>Shared folder for every portable utility. ProgramData so the copies are the
        /// same for any signed-in account, and not %TEMP%, which Disk Cleanup empties.</summary>
        public static string ToolsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RionTweaks", "Utilities");

        private static string LegacyToolsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RionAmdThinner", "Utilities");

        /// <summary>Moves anything downloaded under the old per-user path into <see cref="ToolsDir"/>
        /// so tools already fetched aren't downloaded a second time. Best effort and idempotent.</summary>
        public static void MigrateLegacyDownloads()
        {
            try
            {
                if (!Directory.Exists(LegacyToolsDir)) return;
                Directory.CreateDirectory(ToolsDir);
                // Every file, not just *.exe: these tools keep settings in an .ini beside the exe.
                foreach (string source in Directory.GetFiles(LegacyToolsDir))
                {
                    string target = Path.Combine(ToolsDir, Path.GetFileName(source));
                    if (File.Exists(target)) { try { File.Delete(source); } catch { } continue; }
                    try { File.Move(source, target); } catch { }
                }
                try { if (Directory.GetFileSystemEntries(LegacyToolsDir).Length == 0) Directory.Delete(LegacyToolsDir); } catch { }
            }
            catch (Exception ex) { StaticViewModel.AddDebugMessage(ex, "Could not migrate the old utilities folder"); }
        }

        /// <summary>Full path to an already-downloaded utility, or null. A truncated leftover reads
        /// as missing, so the UI offers to fetch it again instead of failing on launch.</summary>
        public static string FindDownloaded(string fileName, long minSizeBytes = 1024)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)) return null;
            try
            {
                string path = Path.Combine(ToolsDir, fileName);
                return File.Exists(path) && new FileInfo(path).Length >= minSizeBytes ? path : null;
            }
            catch { return null; }
        }

        /// <summary>Launches an already-downloaded utility. Re-checks the signature or pin first:
        /// the file has been on disk since the last run.</summary>
        public static bool LaunchDownloaded(string path, string expectedSha256 = null, string expectedPublisher = null)
        {
            using (var guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!IsAcceptedExecutable(path, expectedSha256, expectedPublisher))
                    throw new InvalidDataException("The cached utility failed signature or fingerprint validation. It was not launched.");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                return true;
            }
        }

        internal static bool IsTrustedExecutable(string path)
        {
            var result = new InstallerValidator().Inspect(path);
            return result.SignatureStatus == SignatureStatus.Valid || result.SignatureStatus == SignatureStatus.ValidButNotAmd;
        }

        internal static bool IsAcceptedPublisher(SignatureStatus status, string actualPublisher, string expectedPublisher)
        {
            bool trusted = status == SignatureStatus.Valid || status == SignatureStatus.ValidButNotAmd;
            // Null preserves legacy callers; production direct-download entries specify a publisher or pin.
            return trusted && (expectedPublisher == null ||
                !string.IsNullOrWhiteSpace(expectedPublisher) &&
                string.Equals(actualPublisher, expectedPublisher, StringComparison.OrdinalIgnoreCase));
        }
        // Explicit catalog pins support the user's curated unsigned tools. A pin is
        // mandatory when supplied, even if a replacement has a valid signature.
        internal static bool IsAcceptedExecutable(string path, string expectedSha256 = null, string expectedPublisher = null)
        {
            if (expectedSha256 == null)
            {
                var result = new InstallerValidator().Inspect(path);
                return IsAcceptedPublisher(result.SignatureStatus, result.Publisher, expectedPublisher);
            }
            using var stream = File.OpenRead(path);
            return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        internal static async Task DownloadFileAsync(HttpClient client, string url, string destination,
            IProgress<double> progress, long minSizeBytes, CancellationToken ct, string expectedSha256 = null, string expectedPublisher = null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Utility downloads require HTTPS.");
            string partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                        throw new InvalidOperationException("Utility download redirected to an insecure connection.");
                    long? total = response.Content.Headers.ContentLength;
                    long read = 0;
                    using (var src = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var dst = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                    {
                        var buffer = new byte[65536];
                        int count;
                        while ((count = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await dst.WriteAsync(buffer, 0, count, ct).ConfigureAwait(false);
                            read += count;
                            progress?.Report(total > 0 ? (double)read / total.Value : -1);
                        }
                    }
                    if (read < minSizeBytes || (total.HasValue && read != total.Value))
                        throw new InvalidDataException("The utility download is incomplete.");
                }
                ct.ThrowIfCancellationRequested();
                if (!IsAcceptedExecutable(partial, expectedSha256, expectedPublisher))
                    throw new InvalidDataException(expectedSha256 == null ? "The utility has no valid signature from the expected publisher. It was not launched." : "The utility does not match the curated SHA-256 fingerprint. It was not launched.");
                File.Move(partial, destination, true);
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
            }
        }

        public static async Task<bool> DownloadAndLaunchAsync(string url, string fileName,
            IProgress<double> progress = null, long minSizeBytes = 1024, CancellationToken ct = default, string expectedSha256 = null, string expectedPublisher = null)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)
                || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !string.Equals(Path.GetExtension(fileName), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A plain executable file name is required.", nameof(fileName));
            Directory.CreateDirectory(ToolsDir);
            string destination = Path.Combine(ToolsDir, fileName);
            if (!File.Exists(destination) || new FileInfo(destination).Length < minSizeBytes || !IsAcceptedExecutable(destination, expectedSha256, expectedPublisher))
                await DownloadFileAsync(Http, url, destination, progress, minSizeBytes, ct, expectedSha256, expectedPublisher).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            // Keep the file read-locked between verification and process creation.
            using (var guard = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!IsAcceptedExecutable(destination, expectedSha256, expectedPublisher))
                    throw new InvalidDataException("The cached utility failed signature or fingerprint validation. It was not launched.");
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(destination) { UseShellExecute = true });
                    return true;
                }
                catch (Exception ex)
                {
                    StaticViewModel.AddDebugMessage(ex, "Could not launch " + fileName);
                    throw new InvalidOperationException("Could not launch " + fileName + ": " + ex.Message, ex);
                }
            }
        }
    }
}

