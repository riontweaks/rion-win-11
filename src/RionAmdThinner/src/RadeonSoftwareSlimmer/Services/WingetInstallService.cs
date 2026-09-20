using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Optimize;

namespace RadeonSoftwareSlimmer.Services;

public static class WingetInstallService
{
	private static string ResolvedPath;

	public static string ExecutablePath
	{
		get
		{
			if (ResolvedPath != null)
			{
				return ResolvedPath;
			}
			ResolvedPath = ShellRunner.RequireExecutable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe"), "WinGet is unavailable at its Windows App Installer alias. Install or repair App Installer and enable its winget app execution alias, then retry.");
			return ResolvedPath;
		}
	}

	public static bool IsAvailable()
	{
		try
		{
			return ShellRunner.RunAsync(ExecutablePath, "--version", null, 10000).GetAwaiter().GetResult()
				.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	public static async Task<bool> InstallAsync(string wingetId, Action<string> onLine, CancellationToken ct = default(CancellationToken), bool interactive = false)
	{
		string value = ((wingetId == "Microsoft.DirectX") ? " --installer-type exe" : "");
		string value2 = (interactive ? "--interactive" : "--silent");
		string arguments = $"install --id {wingetId} -e{value} {value2} --accept-package-agreements --accept-source-agreements --source winget --disable-interactivity";
		return InterpretInstallResult(await ShellRunner.RunAsync(ExecutablePath, arguments, onLine, 600000, ct).ConfigureAwait(continueOnCapturedContext: false));
	}

	internal static bool InterpretInstallResult(ShellRunner.Result result)
	{
		if (result.TimedOut)
		{
			throw new TimeoutException("WinGet timed out. " + result.Output);
		}
		if (result.ExitCode == 0 || result.ExitCode == -1978335189)
		{
			return true;
		}
		throw new InvalidOperationException($"WinGet failed (0x{result.ExitCode:X8}).\n{result.Output?.Trim()}");
	}

	public static async Task<bool> InstallFromStoreAsync(string storeProductId, Action<string> onLine, CancellationToken ct = default(CancellationToken))
	{
		string arguments = "install --id " + storeProductId + " --source msstore -e --silent --accept-package-agreements --accept-source-agreements";
		return (await ShellRunner.RunAsync(ExecutablePath, arguments, onLine, 600000, ct).ConfigureAwait(continueOnCapturedContext: false)).ExitCode == 0;
	}
}
