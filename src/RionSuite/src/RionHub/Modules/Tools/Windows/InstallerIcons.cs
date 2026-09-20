// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Optimize;
using RionHub.Modules.Tools.Debloat;

namespace RionHub.Modules.Tools.Windows;

internal static class InstallerIcons
{
	internal static string Initials(string name)
	{
		return string.Concat(from word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2)
			select word[0]).ToUpperInvariant();
	}

	internal static bool Matches(string displayName, string name)
	{
		if (!displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
		{
			if (displayName.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase) && displayName.Length > name.Length + 1)
			{
				return char.IsDigit(displayName[name.Length + 1]);
			}
			return false;
		}
		return true;
	}

	internal static Dictionary<string, string> ReadDesktopIcons(IEnumerable<string> names)
	{
		string[] source = names.ToArray();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		RegistryHive[] array = new RegistryHive[2]
		{
			RegistryHive.CurrentUser,
			RegistryHive.LocalMachine
		};
		foreach (RegistryHive hKey in array)
		{
			RegistryView[] array2 = new RegistryView[2]
			{
				RegistryView.Registry64,
				RegistryView.Registry32
			};
			foreach (RegistryView view in array2)
			{
				using RegistryKey registryKey = RegistryKey.OpenBaseKey(hKey, view);
				using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
				if (registryKey2 == null)
				{
					continue;
				}
				string[] subKeyNames = registryKey2.GetSubKeyNames();
				foreach (string name in subKeyNames)
				{
					try
					{
						using RegistryKey registryKey3 = registryKey2.OpenSubKey(name);
						object obj = registryKey3?.GetValue("DisplayName");
						string display = obj as string;
						if (display == null || !(registryKey3.GetValue("DisplayIcon") is string value))
						{
							continue;
						}
						foreach (string item in source.Where((string name2) => Matches(display, name2)))
						{
							dictionary.TryAdd(item, value);
						}
					}
					catch (Exception ex) when (((ex is UnauthorizedAccessException || ex is SecurityException || ex is IOException) ? 1 : 0) != 0)
					{
					}
				}
			}
		}
		return dictionary;
	}

	internal static async Task<Dictionary<string, string>> ReadStoreIconsAsync()
	{
		Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		ShellRunner.Result result2 = await ShellRunner.PowerShellAsync("Get-AppxPackage -ErrorAction Stop | ForEach-Object { Write-Output ($_.PackageFamilyName + [char]9 + $_.InstallLocation) }", null, 30000);
		if (result2.ExitCode != 0 || result2.TimedOut)
		{
			return result;
		}
		string[] array = result2.Output.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Trim().Split('\t');
			if (array2.Length == 2)
			{
				result[array2[0]] = StoreAppIconResolver.Resolve(array2[1]);
			}
		}
		return result;
	}
}
