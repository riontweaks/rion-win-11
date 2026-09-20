using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>Copies and verifies an installer into a new, independently movable folder. Never launches it.</summary>
    public static class InstallerFolderExport
    {
        private sealed record ExportFile(string Path, long Bytes, string Sha256);

        public static async Task<string> ExportAsync(string source, string parent, string vendor,
            Func<string, Task> finalize, IProgress<string> progress, CancellationToken cancellationToken)
        {
            if (vendor != "AMD" && vendor != "NVIDIA") throw new ArgumentException("Unknown installer vendor.");
            source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
            parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
            if (!Directory.Exists(source) || !File.Exists(Path.Combine(source, "setup.exe"))) throw new IOException("A complete prepared installer with setup.exe is required.");
            if (!Directory.Exists(parent)) throw new IOException("Choose an existing destination folder.");
            CheckAncestors(source); CheckAncestors(parent);
            if (Within(parent, source)) throw new IOException("Choose a destination outside the extracted installer folder.");
            string name = "Rion-" + vendor + "-Installer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
            string staging = Path.Combine(parent, name + ".partial");
            string destination = Path.Combine(parent, name);
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(staging) || Directory.Exists(destination)) throw new IOException("The export destination already exists.");
            Directory.CreateDirectory(staging);
            try
            {
                var files = new List<ExportFile>();
                await CopyTree(source, source, staging, files, progress, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Validating exported installer selections…");
                await finalize(staging).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await File.WriteAllTextAsync(Path.Combine(staging, "Rion-export-verification.json"), JsonConvert.SerializeObject(new
                {
                    Schema = 1, Vendor = vendor, ExportedUtc = DateTime.UtcNow,
                    Note = "These hashes verify copied source files before export metadata and launch instructions were generated. No installer was launched.", Files = files
                }, Formatting.Indented), cancellationToken).ConfigureAwait(false);
                CheckAncestors(parent);
                Directory.Move(staging, destination);
                return destination;
            }
            catch (Exception ex)
            {
                // Do not delete user-selected trees on an error. A partial export is never promoted to ready.
                throw new IOException((ex is OperationCanceledException ? "Export cancelled." : "Export failed: " + ex.Message) +
                    " Incomplete files are retained in " + staging + ". Do not run that incomplete copy.", ex);
            }
        }

        private static bool Within(string candidate, string root) => candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static void CheckAncestors(string path)
        {
            for (var dir = new DirectoryInfo(path); dir != null; dir = dir.Parent)
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked folders and junctions are not supported for installer export: " + dir.FullName);
        }

        private static async Task CopyTree(string root, string current, string destination, List<ExportFile> files,
            IProgress<string> progress, CancellationToken cancellationToken)
        {
            CheckAncestors(current);
            foreach (string entry in Directory.EnumerateFileSystemEntries(current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked installer files cannot be exported: " + entry);
                string relative = Path.GetRelativePath(root, entry);
                string target = Path.Combine(destination, relative);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    Directory.CreateDirectory(target);
                    await CopyTree(root, entry, destination, files, progress, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                progress?.Report("Copying and verifying " + relative);
                // Hold the source without write sharing through copy and hash verification.
                using var input = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
                {
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                input.Position = 0;
                string before = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false));
                using var check = File.OpenRead(target);
                string after = Convert.ToHexString(await SHA256.HashDataAsync(check, cancellationToken).ConfigureAwait(false));
                if (before != after) throw new IOException("Copy verification failed: " + relative);
                files.Add(new(relative, input.Length, after));
            }
        }
    }
}
