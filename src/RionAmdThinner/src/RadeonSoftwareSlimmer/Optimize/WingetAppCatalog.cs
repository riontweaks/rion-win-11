using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Optimize
{
    /// <summary>One entry in the Windows tab's Ninite-style installer — a plain winget package.
    /// IDs confirmed live against a real machine's `winget search`/`winget show`, not memorized;
    /// re-verify with `winget show &lt;id&gt;` if one ever fails to resolve, since winget's catalog
    /// changes over time.</summary>
    public sealed class WingetAppEntry
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string WingetId { get; set; }
    }

    public static class WingetAppCatalog
    {
        public static IReadOnlyList<WingetAppEntry> Apps { get; } = new List<WingetAppEntry>
        {
            // Browsers
            new WingetAppEntry { Name = "Google Chrome", Description = "Web browser.", WingetId = "Google.Chrome" },
            new WingetAppEntry { Name = "Mozilla Firefox", Description = "Web browser.", WingetId = "Mozilla.Firefox" },
            new WingetAppEntry { Name = "Brave", Description = "Privacy-focused web browser.", WingetId = "Brave.Brave" },
            new WingetAppEntry { Name = "Opera", Description = "Web browser.", WingetId = "Opera.Opera" },

            // Communication / media
            new WingetAppEntry { Name = "Discord", Description = "Voice, video, and text chat.", WingetId = "Discord.Discord" },
            new WingetAppEntry { Name = "Spotify", Description = "Music streaming.", WingetId = "Spotify.Spotify" },
            new WingetAppEntry { Name = "Vencord", Description = "Discord client mod.", WingetId = "Vendicated.Vencord" },
            new WingetAppEntry { Name = "Zoom", Description = "Video conferencing.", WingetId = "Zoom.Zoom" },

            // Gaming / launchers
            new WingetAppEntry { Name = "Steam", Description = "Valve's game launcher and store.", WingetId = "Valve.Steam" },
            new WingetAppEntry { Name = "Epic Games Launcher", Description = "Epic's game launcher and store.", WingetId = "EpicGames.EpicGamesLauncher" },
            new WingetAppEntry { Name = "GOG Galaxy", Description = "GOG's game launcher.", WingetId = "GOG.Galaxy" },
            new WingetAppEntry { Name = "Battle.net", Description = "Blizzard's game launcher.", WingetId = "Blizzard.BattleNet" },
            new WingetAppEntry { Name = "EA App", Description = "EA's game launcher.", WingetId = "ElectronicArts.EADesktop" },
            new WingetAppEntry { Name = "Ubisoft Connect", Description = "Ubisoft's game launcher.", WingetId = "Ubisoft.Connect" },
            new WingetAppEntry { Name = "Playnite", Description = "Unifies all your game libraries into one launcher.", WingetId = "Playnite.Playnite" },

            // Utilities
            new WingetAppEntry { Name = "7-Zip", Description = "File archiver.", WingetId = "7zip.7zip" },
            new WingetAppEntry { Name = "WinRAR", Description = "File archiver.", WingetId = "WinRAR.WinRAR" },
            new WingetAppEntry { Name = "VLC", Description = "Media player.", WingetId = "VideoLAN.VLC" },
            new WingetAppEntry { Name = "OBS Studio", Description = "Screen recording and streaming.", WingetId = "OBSProject.OBSStudio" },
            new WingetAppEntry { Name = "ShareX", Description = "Screenshot and screen recording utility.", WingetId = "ShareX.ShareX" },
            new WingetAppEntry { Name = "Notepad++", Description = "Text editor.", WingetId = "Notepad++.Notepad++" },
            new WingetAppEntry { Name = "qBittorrent", Description = "Torrent client.", WingetId = "qBittorrent.qBittorrent" },
            new WingetAppEntry { Name = "Malwarebytes", Description = "Anti-malware scanner.", WingetId = "Malwarebytes.Malwarebytes" },
            new WingetAppEntry { Name = "TeamViewer", Description = "Remote desktop access.", WingetId = "TeamViewer.TeamViewer" },
            new WingetAppEntry { Name = "AnyDesk", Description = "Remote desktop access.", WingetId = "AnyDesk.AnyDesk" },
            new WingetAppEntry { Name = "Everything", Description = "Instant file search.", WingetId = "voidtools.Everything" },
            new WingetAppEntry { Name = "PowerToys", Description = "Microsoft's official Windows utility suite.", WingetId = "Microsoft.PowerToys" },

            // Hardware / GPU tools
            new WingetAppEntry { Name = "MSI Afterburner", Description = "GPU overclocking and monitoring.", WingetId = "Guru3D.Afterburner" },
            new WingetAppEntry { Name = "CPU-Z", Description = "CPU/system information.", WingetId = "CPUID.CPU-Z" },
            new WingetAppEntry { Name = "HWiNFO", Description = "Detailed hardware information and monitoring.", WingetId = "REALiX.HWiNFO" },
            new WingetAppEntry { Name = "CrystalDiskInfo", Description = "Drive health monitoring.", WingetId = "CrystalDewWorld.CrystalDiskInfo" },
        };

        /// <summary>Individually selectable runtimes. Major .NET versions install side by side;
        /// install the runtime family and major version required by the application.</summary>
        public static IReadOnlyList<WingetAppEntry> Runtimes { get; } = new List<WingetAppEntry>
        {
            // IDs verified against microsoft/winget-pkgs on 2026-09-19.
            new WingetAppEntry { Name = ".NET Desktop Runtime 10", Description = "WPF and Windows Forms apps; includes the base .NET runtime.", WingetId = "Microsoft.DotNet.DesktopRuntime.10" },
            new WingetAppEntry { Name = ".NET Runtime 10", Description = "Console and service apps; desktop apps need Desktop Runtime.", WingetId = "Microsoft.DotNet.Runtime.10" },
            new WingetAppEntry { Name = ".NET ASP.NET Core Runtime 10", Description = "ASP.NET Core server apps; not needed for ordinary desktop apps.", WingetId = "Microsoft.DotNet.AspNetCore.10" },
            new WingetAppEntry { Name = ".NET Desktop Runtime 9", Description = "WPF and Windows Forms apps; includes the base .NET runtime.", WingetId = "Microsoft.DotNet.DesktopRuntime.9" },
            new WingetAppEntry { Name = ".NET Runtime 9", Description = "Console and service apps; desktop apps need Desktop Runtime.", WingetId = "Microsoft.DotNet.Runtime.9" },
            new WingetAppEntry { Name = ".NET ASP.NET Core Runtime 9", Description = "ASP.NET Core server apps; not needed for ordinary desktop apps.", WingetId = "Microsoft.DotNet.AspNetCore.9" },
            new WingetAppEntry { Name = ".NET Desktop Runtime 8", Description = "WPF and Windows Forms apps; includes the base .NET runtime.", WingetId = "Microsoft.DotNet.DesktopRuntime.8" },
            new WingetAppEntry { Name = ".NET Runtime 8", Description = "Console and service apps; desktop apps need Desktop Runtime.", WingetId = "Microsoft.DotNet.Runtime.8" },
            new WingetAppEntry { Name = ".NET ASP.NET Core Runtime 8", Description = "ASP.NET Core server apps; not needed for ordinary desktop apps.", WingetId = "Microsoft.DotNet.AspNetCore.8" },
            new WingetAppEntry { Name = "VC++ 2005 x86", Description = "Visual C++ 2005 Redistributable.", WingetId = "Microsoft.VCRedist.2005.x86" },
            new WingetAppEntry { Name = "VC++ 2005 x64", Description = "Visual C++ 2005 Redistributable.", WingetId = "Microsoft.VCRedist.2005.x64" },
            new WingetAppEntry { Name = "VC++ 2008 x86", Description = "Visual C++ 2008 Redistributable.", WingetId = "Microsoft.VCRedist.2008.x86" },
            new WingetAppEntry { Name = "VC++ 2008 x64", Description = "Visual C++ 2008 Redistributable.", WingetId = "Microsoft.VCRedist.2008.x64" },
            new WingetAppEntry { Name = "VC++ 2010 x86", Description = "Visual C++ 2010 Redistributable.", WingetId = "Microsoft.VCRedist.2010.x86" },
            new WingetAppEntry { Name = "VC++ 2010 x64", Description = "Visual C++ 2010 Redistributable.", WingetId = "Microsoft.VCRedist.2010.x64" },
            new WingetAppEntry { Name = "VC++ 2012 x86", Description = "Visual C++ 2012 Redistributable.", WingetId = "Microsoft.VCRedist.2012.x86" },
            new WingetAppEntry { Name = "VC++ 2012 x64", Description = "Visual C++ 2012 Redistributable.", WingetId = "Microsoft.VCRedist.2012.x64" },
            new WingetAppEntry { Name = "VC++ 2013 x86", Description = "Visual C++ 2013 Redistributable.", WingetId = "Microsoft.VCRedist.2013.x86" },
            new WingetAppEntry { Name = "VC++ 2013 x64", Description = "Visual C++ 2013 Redistributable.", WingetId = "Microsoft.VCRedist.2013.x64" },
            new WingetAppEntry { Name = "VC++ 2015-2022 x86", Description = "Visual C++ 2015-2022 Redistributable.", WingetId = "Microsoft.VCRedist.2015+.x86" },
            new WingetAppEntry { Name = "VC++ 2015-2022 x64", Description = "Visual C++ 2015-2022 Redistributable.", WingetId = "Microsoft.VCRedist.2015+.x64" },
            new WingetAppEntry { Name = "DirectX Runtime", Description = "DirectX End-User Runtime.", WingetId = "Microsoft.DirectX" },
        };
    }
}
