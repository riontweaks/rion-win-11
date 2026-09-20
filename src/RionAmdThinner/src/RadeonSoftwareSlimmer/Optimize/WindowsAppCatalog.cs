using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Optimize
{
    public enum WindowsAppMechanism
    {
        /// <summary>Plain winget install (OneDrive isn't a UWP package).</summary>
        Winget,
        /// <summary>Inbox UWP component — winget doesn't cover these directly; repaired in place
        /// by package family name via <see cref="Services.AppxRepairService"/>, falling back to a
        /// Microsoft Store install by product id if the package is fully absent.</summary>
        AppxRepair,
        /// <summary>Reuses FixCatalog's existing "store-fix" (re-register + service reset +
        /// wsreset) rather than a generic Appx path — Microsoft Store repairing itself needs more
        /// than a re-register.</summary>
        StoreFix,
    }

    public sealed class WindowsAppEntry
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public WindowsAppMechanism Mechanism { get; set; }
        /// <summary>Winget id (Mechanism=Winget) or package family name (Mechanism=AppxRepair).</summary>
        public string Identifier { get; set; }
        /// <summary>AppxRepair only: Microsoft Store product id, used as the fallback when the
        /// package is fully absent and re-registering in place isn't possible — confirmed live
        /// this session as the only mechanism that actually fetches a removed inbox app fresh.</summary>
        public string StoreProductId { get; set; }
    }

    /// <summary>Apps typically removed by Debloat, offered here to bring back. Package family
    /// names and Store product ids below were confirmed directly against a real machine this
    /// session (Get-AppxPackage / winget show), not assumed.</summary>
    public static class WindowsAppCatalog
    {
        public static IReadOnlyList<WindowsAppEntry> All { get; } = new List<WindowsAppEntry>
        {
            new WindowsAppEntry { Name = "OneDrive", Description = "Microsoft's cloud storage sync client.", Mechanism = WindowsAppMechanism.Winget, Identifier = "Microsoft.OneDrive" },
            new WindowsAppEntry { Name = "Microsoft Store", Description = "Fixes and ensures the Store itself is registered.", Mechanism = WindowsAppMechanism.StoreFix },
            new WindowsAppEntry { Name = "Xbox App", Description = "Xbox app for PC gaming.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.GamingApp_8wekyb3d8bbwe", StoreProductId = "9MV0B5HZVK9Z" },
            new WindowsAppEntry { Name = "Xbox Game Bar", Description = "In-game screenshots, recording, and widgets.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.XboxGamingOverlay_8wekyb3d8bbwe", StoreProductId = "9NZKPSTSNW4P" },
            new WindowsAppEntry { Name = "Paint", Description = "Windows' built-in image editor.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.Paint_8wekyb3d8bbwe", StoreProductId = "9PCFS5B6T72H" },
            new WindowsAppEntry { Name = "Snipping Tool", Description = "Windows' built-in screenshot tool.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.ScreenSketch_8wekyb3d8bbwe", StoreProductId = "9MZ95KL8MR0L" },
            // Product IDs and package families verified from Microsoft Store metadata, 2026-09-19.
            new WindowsAppEntry { Name = "Windows Calculator", Description = "Basic, scientific, and programmer calculator.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.WindowsCalculator_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFHVN5" },
            new WindowsAppEntry { Name = "Microsoft Photos", Description = "View and organize photos.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.Windows.Photos_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFJBH4" },
            new WindowsAppEntry { Name = "Windows Notepad", Description = "Plain-text editor.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.WindowsNotepad_8wekyb3d8bbwe", StoreProductId = "9MSMLRH6LZF3" },
            new WindowsAppEntry { Name = "Microsoft Sticky Notes", Description = "Desktop notes and optional account sync.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe", StoreProductId = "9NBLGGH4QGHW" },
            new WindowsAppEntry { Name = "Windows Camera", Description = "Take photos and record video.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.WindowsCamera_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFJBBG" },
            new WindowsAppEntry { Name = "Windows Sound Recorder", Description = "Record audio from your microphone.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.WindowsSoundRecorder_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFHWKN" },
            new WindowsAppEntry { Name = "Windows Clock", Description = "Alarms, timers, and focus sessions.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.WindowsAlarms_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFJ3PR" },
            new WindowsAppEntry { Name = "Microsoft To Do: Lists, Tasks & Reminders", Description = "Task lists and reminders.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.Todos_8wekyb3d8bbwe", StoreProductId = "9NBLGGH5R558" },
            new WindowsAppEntry { Name = "Quick Assist", Description = "Remote assistance with a person you trust.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "MicrosoftCorporationII.QuickAssist_8wekyb3d8bbwe", StoreProductId = "9P7BP5VNWKX5" },
            new WindowsAppEntry { Name = "Phone Link", Description = "Connect your phone for messages and notifications.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.YourPhone_8wekyb3d8bbwe", StoreProductId = "9NMPJ99VJBWV" },
            new WindowsAppEntry { Name = "Microsoft Clipchamp", Description = "Video editor.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Clipchamp.Clipchamp_yxz26nhyzhsrt", StoreProductId = "9P1J8S7CCWWT" },
            new WindowsAppEntry { Name = "Raw Image Extension", Description = "RAW image decoding for supported cameras.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.RawImageExtension_8wekyb3d8bbwe", StoreProductId = "9NCTDW2W1BH8" },
            new WindowsAppEntry { Name = "Windows Terminal", Description = "Terminal for PowerShell and command-line tools.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.WindowsTerminal_8wekyb3d8bbwe", StoreProductId = "9N0DX20HK701" },
            new WindowsAppEntry { Name = "MSN Weather", Description = "Weather forecasts.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.BingWeather_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFJ3Q2" },
            new WindowsAppEntry { Name = "Microsoft News", Description = "News reader.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.BingNews_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFHVFW" },
            new WindowsAppEntry { Name = "Microsoft Solitaire Collection", Description = "Solitaire and other card games.", Mechanism = WindowsAppMechanism.AppxRepair, Identifier = "Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe", StoreProductId = "9WZDNCRFHWD2" },
        };
    }
}
