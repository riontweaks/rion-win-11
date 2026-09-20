using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Optimize;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>Reinstalls/repairs an inbox UWP component (Xbox app, Xbox Game Bar, Paint,
    /// Snipping Tool) — winget doesn't cover these as plain packages, they're Store-mediated Appx
    /// packages. Two-tier: if the package is merely present-but-broken, re-register it in place
    /// (cheap, no network) — same PowerShell-via-ShellRunner shape FixCatalog's "store-fix" already
    /// uses. If it's fully absent (confirmed on Ryan's machine for Paint/Game Bar — Debloat's
    /// KeepAlways list protects them, but they can still end up missing after a clean install or a
    /// Windows feature update), `Add-AppxPackage -RegisterByFamilyName` fails outright with no
    /// local source to register from — it does NOT download anything. The only mechanism that
    /// actually works from a fully-removed state is `winget install --source msstore`, confirmed
    /// live against a real machine this session (Paint went from completely absent to installed
    /// with a plain silent winget call, no interactive Store prompt).</summary>
    public static class AppxRepairService
    {
        public static async Task<bool> ReinstallAsync(string packageFamilyName, string storeProductId, string displayName, System.Action<string> onLine, CancellationToken ct = default)
        {
            string script = $@"
$pkg = Get-AppxPackage -AllUsers | Where-Object {{ $_.PackageFamilyName -eq '{packageFamilyName}' }}
if ($pkg) {{
    try {{
        Add-AppxPackage -DisableDevelopmentMode -Register ""$($pkg.InstallLocation)\AppXManifest.xml"" -ErrorAction Stop
        Write-Output ""OK`tre-registered {displayName}""
    }} catch {{
        Write-Output ""NEEDS-STORE`t$($_.Exception.Message)""
    }}
}} else {{
    Write-Output ""NEEDS-STORE`tnot present""
}}
";
            ShellRunner.Result res = await ShellRunner.PowerShellAsync(script, onLine, timeoutMs: 2 * 60 * 1000, ct: ct).ConfigureAwait(false);
            string output = res.Output ?? "";
            if (output.IndexOf("OK\t", System.StringComparison.Ordinal) >= 0)
                return true;

            // In-place re-register didn't work (or nothing to re-register) — fall back to the
            // mechanism that actually downloads it fresh.
            onLine?.Invoke($"{displayName}: not present or couldn't be re-registered in place — installing from the Microsoft Store instead.");
            return await WingetInstallService.InstallFromStoreAsync(storeProductId, onLine, ct).ConfigureAwait(false);
        }
    }
}
