using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Services;

namespace NvidiaDriverTool.Services
{
    /// <summary>Validates a real extracted package and persists selections. Never starts setup.</summary>
    public static class NvidiaPackagePreparation
    {
        public static Task<string> ExportAsync(string root, string destination, IEnumerable<string> exclusions,
            IProgress<string> progress, System.Threading.CancellationToken cancellationToken, NvidiaInstallationOptions options = null)
        {
            var excluded = exclusions.ToArray();
            return InstallerFolderExport.ExportAsync(root, destination, "NVIDIA", async folder =>
            {
                var prepared = await PrepareAsync(folder, excluded, options).ConfigureAwait(false);
                // Export metadata and launch paths must remain valid after the folder is moved.
                var plan = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(prepared.PlanPath).ConfigureAwait(false));
                plan["Setup"] = "setup.exe";
                await File.WriteAllTextAsync(prepared.PlanPath, plan.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(folder, "Install-selected-packages.cmd"),
                    NvidiaInstallPlan.ExportLauncher(NvidiaComponentManifest.Parse(folder), excluded, options)).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(folder, "README-Rion.txt"),
                    "NVIDIA customized installer\r\n\r\nRun Install-selected-packages.cmd as administrator to install with the saved package choices.\r\n" + (options ?? new NvidiaInstallationOptions()).Description + "\r\nRunning setup.exe directly does NOT apply the saved exclusions.\r\nKeep this entire folder together; Rion is not needed. Vendor files are preserved, so exclusions do not reduce folder size.\r\nService/task choices tied to excluded packages are reflected; live service startup changes and GPU tweaks are not included.\r\nUse only on hardware and Windows versions supported by this driver.\r\nNo installation was performed during export.\r\n").ConfigureAwait(false);
            }, progress, cancellationToken);
        }

        public sealed record PreparedPackage(string PlanPath, string Arguments, int Kept, int Excluded);

        public static Task<PreparedPackage> PrepareAsync(string root, IEnumerable<string> exclusions, NvidiaInstallationOptions options = null)
        {
            // Snapshot UI selections before moving onto a worker thread.
            string[] excluded = exclusions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Analyze a package first.");
                root = Path.GetFullPath(root);
                string setup = Path.Combine(root, "setup.exe");
                var identity = new InstallerValidator().Inspect(setup);
                if (!NvidiaInstallPlan.IsTrusted(identity))
                    throw new InvalidOperationException("The extracted setup does not have a valid NVIDIA Corporation signature.");
                var components = NvidiaComponentManifest.Parse(root);
                string arguments = NvidiaInstallPlan.Arguments(components, excluded, options);
                var files = Directory.EnumerateFiles(root, "*.nvi", SearchOption.AllDirectories)
                    .Append(Path.Combine(root, "setup.cfg")).Append(setup)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new { File = Path.GetRelativePath(root, p), Sha256 = Hash(p) }).ToArray();
                string plan = Path.Combine(root, "Rion-prepared-package.json");
                string json = JsonSerializer.Serialize(new
                {
                    Schema = 2, Options = options ?? new NvidiaInstallationOptions(), PreparedUtc = DateTimeOffset.UtcNow,
                    Setup = setup, Publisher = identity.Publisher, Arguments = arguments,
                    Keeping = components.Where(c => !excluded.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).Select(c => c.Name),
                    Excluding = excluded, Components = components, VerifiedFiles = files,
                    Note = "Preparation only. Vendor files are unchanged. Exclusions are passed to NVIDIA setup; no installer was launched and no existing services were changed."
                }, new JsonSerializerOptions { WriteIndented = true });
                string temporary = plan + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, json);
                File.Move(temporary, plan, true);
                return new PreparedPackage(plan, arguments, components.Count - excluded.Length, excluded.Length);
            });
        }

        private static string Hash(string file)
        {
            using var stream = File.OpenRead(file);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
