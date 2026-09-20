namespace RadeonSoftwareSlimmer.Models
{
    public enum StartupLocation
    {
        CurrentUserRun,
        LocalMachineRun,
        LocalMachineRun32,
        CurrentUserStartupFolder,
        CommonStartupFolder,
    }

    /// <summary>One logon auto-start item. Disable is done the same way Task Manager does it —
    /// a 12-byte "disabled" blob in the matching StartupApproved key — so the Run value is never lost.</summary>
    public sealed class StartupEntry
    {
        public StartupEntry(string name, string command, StartupLocation location, bool isEnabled)
        {
            Name = name;
            Command = command;
            Location = location;
            IsEnabled = isEnabled;
        }

        public string Name { get; }
        public string Command { get; }
        public StartupLocation Location { get; }
        public bool IsEnabled { get; }

        public bool IsRegistryEntry =>
            Location == StartupLocation.CurrentUserRun ||
            Location == StartupLocation.LocalMachineRun ||
            Location == StartupLocation.LocalMachineRun32;

        public bool RequiresElevation =>
            Location == StartupLocation.LocalMachineRun ||
            Location == StartupLocation.LocalMachineRun32 ||
            Location == StartupLocation.CommonStartupFolder;

        public string LocationLabel
        {
            get
            {
                switch (Location)
                {
                    case StartupLocation.CurrentUserRun: return "HKCU\\…\\Run";
                    case StartupLocation.LocalMachineRun: return "HKLM\\…\\Run";
                    case StartupLocation.LocalMachineRun32: return "HKLM\\…\\Run (WOW6432Node)";
                    case StartupLocation.CurrentUserStartupFolder: return "Startup folder (you)";
                    case StartupLocation.CommonStartupFolder: return "Startup folder (all users)";
                    default: return Location.ToString();
                }
            }
        }
    }
}
