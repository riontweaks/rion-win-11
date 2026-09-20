namespace NvidiaDriverTool.ViewModels
{
    /// <summary>
    /// Mirrors <c>RadeonSoftwareSlimmer.Models.WizardStep</c>'s shape but a shorter flow — no
    /// separate "Post-Install" rail tool yet (AMD's service/task trimming has no NVIDIA
    /// equivalent curated yet; the Tools > Tweaks tab already covers general Windows tweaks).
    /// </summary>
    public enum NvidiaWizardStep
    {
        Welcome = 0,
        SelectDriver = 1,
        Analyze = 2,
        Customize = 3,
        Background = 4,
        Review = 5,
        Install = 6,
        PostInstall = 7,
        Finish = 8,
    }
}
