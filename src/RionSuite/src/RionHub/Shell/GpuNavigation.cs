using NvidiaDriverTool.ViewModels;
using RadeonSoftwareSlimmer.Models;

namespace RionHub.Shell;

/// <summary>Navigation metadata must not require native providers or driver inventory.</summary>
internal static class GpuNavigation
{
    internal static readonly (WizardStep Step, string Title)[] AmdSteps =
    [
        (WizardStep.Welcome, "Welcome"),
        (WizardStep.SelectDriver, "Select Driver"),
        (WizardStep.Analyze, "Analyze Installer"),
        (WizardStep.Customize, "Customize Installation"),
        (WizardStep.Review, "Review and Prepare"),
        (WizardStep.Configure, "Choose Services & Tweaks"),
        (WizardStep.Install, "Install and Verify"),
        (WizardStep.Finish, "Finish and Export")
    ];

    internal static readonly (NvidiaWizardStep Step, string Title)[] NvidiaSteps =
    [
        (NvidiaWizardStep.Welcome, "Welcome"),
        (NvidiaWizardStep.SelectDriver, "Select Driver"),
        (NvidiaWizardStep.Analyze, "Analyze"),
        (NvidiaWizardStep.Customize, "Choose Components"),
        (NvidiaWizardStep.Background, "Services & Tasks"),
        (NvidiaWizardStep.Review, "Review"),
        (NvidiaWizardStep.Install, "Install"),
        (NvidiaWizardStep.PostInstall, "Post-Install"),
        (NvidiaWizardStep.Finish, "Finish")
    ];

    internal static bool CanOpenNvidiaStep(bool reachable, bool currentCanGoBack,
        NvidiaWizardStep current, NvidiaWizardStep requested) =>
        reachable && (currentCanGoBack || current == NvidiaWizardStep.Welcome || current == requested);
}
