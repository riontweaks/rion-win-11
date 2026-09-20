using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Optimize;

public static class UtilityToolCatalog
{
	public static IReadOnlyList<UtilityToolEntry> All { get; } = new List<UtilityToolEntry>
	{
		new UtilityToolEntry
		{
			Name = "MSI Mode Utility v3",
			Description = "Inspect device interrupt modes. Download and guidance from the author's release post.",
			Mechanism = UtilityMechanism.DownloadPage,
			Source = "https://forums.guru3d.com/threads/windows-line-based-vs-message-signaled-based-interrupts-msi-tool.378044/"
		},
		new UtilityToolEntry
		{
			Name = "Process Explorer",
			Description = "Inspect running processes, handles and loaded libraries.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://live.sysinternals.com/procexp64.exe",
			FileName = "procexp64.exe",
			ExpectedPublisher = "Microsoft Corporation"
		},
		new UtilityToolEntry
		{
			Name = "TCPView",
			Description = "See which apps have network connections open.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://live.sysinternals.com/Tcpview64.exe",
			FileName = "Tcpview64.exe",
			ExpectedPublisher = "Microsoft Corporation"
		},
		new UtilityToolEntry
		{
			Name = "NVCleanstall",
			Description = "Slims down NVIDIA driver installs — pick only the components you want.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://raw.githubusercontent.com/riontweaks/Files/eea4958175c196e92c25ae7d8110039b16b113cf/NVCleanstall_1.19.0.exe",
			Sha256 = "9DD36EF956AF927CF41FA441F91B329A7973E13965E4E7D70E6FA9C1DF1CADE6",
			FileName = "NVCleanstall.exe"
		},
		new UtilityToolEntry
		{
			Name = "Device Cleanup",
			Description = "Removes leftover/ghost device entries from Device Manager.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://raw.githubusercontent.com/riontweaks/Files/eea4958175c196e92c25ae7d8110039b16b113cf/DeviceCleanup.exe",
			Sha256 = "51BE83B9D14DD68E0293569C58159ADDEFDCCB110115E04725FA55813808E862",
			FileName = "DeviceCleanup.exe"
		},
		new UtilityToolEntry
		{
			Name = "Autoruns",
			Description = "Sysinternals tool for managing what launches at startup.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://live.sysinternals.com/Autoruns64.exe",
			FileName = "Autoruns64.exe",
			ExpectedPublisher = "Microsoft Corporation"
		},
		new UtilityToolEntry
		{
			Name = "Windows Update Blocker (WUB)",
			Description = "Fully disables and locks out Windows Update.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://raw.githubusercontent.com/riontweaks/Files/eea4958175c196e92c25ae7d8110039b16b113cf/Wub.exe",
			Sha256 = "AAC1123F17F8569A36BF93876CEA30E15103FD2379B401A79129A2A6E7285AC2",
			FileName = "Wub.exe"
		},
		new UtilityToolEntry
		{
			Name = "O&O ShutUp10",
			Description = "Granular privacy/telemetry control panel.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://dl5.oo-software.com/files/ooshutup10/OOSU10.exe",
			FileName = "OOSU10.exe",
			ExpectedPublisher = "O&O Software GmbH",
			MinSizeBytes = 1000000L,
			FallbackWingetId = "OO-Software.ShutUp10",
			ExecutableHints = new string[2] { "OOSU10.exe", "shutup10.exe" }
		},
		new UtilityToolEntry
		{
			Name = "Display Driver Uninstaller (DDU)",
			Description = "Completely removes GPU drivers — best run from Safe Mode.",
			Mechanism = UtilityMechanism.Winget,
			Source = "Wagnardsoft.DisplayDriverUninstaller",
			ExpectedPublisher = "Wagnardsoft",
			ExecutableHints = new string[2] { "Display Driver Uninstaller.exe", "DDU.exe" }
		},
		new UtilityToolEntry
		{
			Name = "NVIDIA Profile Inspector",
			Description = "Exposes hidden NVIDIA driver profile settings.",
			Mechanism = UtilityMechanism.DirectDownload,
			Source = "https://raw.githubusercontent.com/riontweaks/Files/eea4958175c196e92c25ae7d8110039b16b113cf/nvidiaProfileInspector.exe",
			Sha256 = "1233487EA4DB928EE062F12B00A6EDA01445D001AB55566107234DEA4DC65872",
			FileName = "nvidiaProfileInspector.exe",
			FallbackWingetId = "Orbmu2k.nvidiaProfileInspector",
			ExecutableHints = new string[2] { "nvidiaProfileInspector.exe", "nvpi.exe" }
		}
	};
}
