namespace NvidiaDriverTool.Services
{
    /// <summary>Documented Installer 2.0 switches. Immutable so exports capture one selection.</summary>
    public sealed record NvidiaInstallationOptions(bool Unattended = true, bool CleanInstall = false)
    {
        public string Description => (Unattended ? "Unattended installation" : "Interactive NVIDIA setup")
            + (CleanInstall ? "; reset previous NVIDIA settings" : "; preserve previous NVIDIA settings")
            + "; automatic reboot suppressed. Restart manually if required.";
    }
}
