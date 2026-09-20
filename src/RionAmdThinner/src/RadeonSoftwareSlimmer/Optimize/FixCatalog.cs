using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Intefaces;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.Optimize
{
    public sealed class FixResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public static FixResult Ok(string message) => new FixResult { Success = true, Message = message };
        public static FixResult Fail(string message) => new FixResult { Success = false, Message = message };
    }

    /// <summary>One named one-click repair. Unlike <see cref="SystemTweak"/> these aren't a single
    /// registry value toggle — each is its own small script of service/registry/appx fixes — so
    /// they're modeled as a plain async action rather than forced into the tweak shape.</summary>
    public sealed class FixDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        /// <summary>Segoe MDL2 glyph, or "nvidia" as a sentinel meaning "use the NVIDIA icon asset"
        /// instead of a glyph (see FixesView's icon template).</summary>
        public string Glyph { get; set; }

        /// <summary>onProgress reports (current, total) step counts for fixes with a statically
        /// known step count (WiFi, Bluetooth, Windows Update, Defender); the two PowerShell-driven
        /// fixes (Store, NVIDIA Control Panel) never call it — their package count isn't known
        /// ahead of time, so the caller shows an indeterminate progress bar instead of a fake total.</summary>
        public Func<IRegistry, Action<string>, Action<int, int>, CancellationToken, Task<FixResult>> Run { get; set; }
    }

    public static class FixCatalog
    {
        public static readonly FixDefinition[] All =
        {
            new FixDefinition
            {
                Id = "store-fix",
                Title = "Microsoft Store Fix",
                Description = "Fixes the Store when apps won't download or install.",
                Glyph = "",
                Run = async (registry, onLine, onProgress, ct) =>
                {
                    var backup = new BackupSession { Description = "Microsoft Store Fix" };
                    foreach (var (svc, start) in new[] { ("bits", 2), ("cryptsvc", 2), ("appxsvc", 3), ("wuauserv", 3) })
                        ServiceTrimCatalog.SetStart(registry, svc, start, backup, "Fixes");
                    backup.Save();
                    // Package count isn't known ahead of time — leave onProgress uncalled so the
                    // caller shows an indeterminate bar instead of a fake total.

                    const string script = @"
Get-AppxPackage -AllUsers Microsoft.WindowsStore | ForEach-Object {
    try { Add-AppxPackage -DisableDevelopmentMode -Register ""$($_.InstallLocation)\AppXManifest.xml"" -ErrorAction Stop; Write-Output ""OK`t$($_.PackageFullName)"" }
    catch { Write-Output ""FAIL`t$($_.PackageFullName)`t$($_.Exception.Message)"" }
}
Start-Process -FilePath wsreset.exe -WindowStyle Hidden -Wait
Write-Output ""DONE""
";
                    var res = await ShellRunner.PowerShellAsync(script, onLine, timeoutMs: 3 * 60 * 1000, ct: ct).ConfigureAwait(false);
                    return res.ExitCode == 0
                        ? FixResult.Ok("Microsoft Store re-registered and its services reset.")
                        : FixResult.Fail("Store fix finished with errors — see the log above.");
                },
            },
        };
    }
}
