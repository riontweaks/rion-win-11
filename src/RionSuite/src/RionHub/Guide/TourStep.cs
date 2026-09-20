using System.Collections.Generic;

namespace RionHub.Guide;

/// <summary>One stop on a guided tour.</summary>
public sealed class TourStep
{
    /// <summary>Nav destination — see <see cref="ShellViewModel.NavigateToKey"/>. null = stay put.</summary>
    public string NavKey { get; set; }

    /// <summary>The <see cref="Tour.Id"/> of the control to spotlight. null = centred bubble, no spotlight.</summary>
    public string TargetId { get; set; }

    public string Number { get; set; }
    public string Heading { get; set; }
    public string Body { get; set; }
}

public static class TourDefinitions
{
    public static IReadOnlyList<TourStep> For(string topic) =>
        topic == "adrenaline" ? Adrenaline() : Driver();

    // NavKeys: "step:<WizardStep>", "tool:post-install", "tab:<TabTitle>", or a top-level title.
    public static IReadOnlyList<TourStep> Driver() => new List<TourStep>
    {
        new TourStep
        {
            NavKey = "Welcome", TargetId = null, Number = "1", Heading = "The Driver Tool",
            Body = "This tool takes an official AMD driver package, lets you strip out the parts you don't "
                   + "want, installs it, then trims AMD's background footprint. Nothing runs silently and "
                   + "everything is reversible. Use Next to walk the flow, or Skip to leave the tour.",
        },
        new TourStep
        {
            NavKey = "Select Driver", TargetId = "select-driver-existing", Number = "2", Heading = "Select Driver",
            Body = "Point the tool at the driver you want. Use 'Browse…' to pick an AMD installer .exe "
                   + "you've already downloaded, or 'Download full GPU package' to fetch the current "
                   + "Adrenalin package for your exact card from amd.com. Then 'Validate' checks it's a "
                   + "genuine AMD-signed package. Use the full package, not the small auto-detect one — "
                   + "only the full one can be slimmed.",
        },
        new TourStep
        {
            NavKey = "Analyze Installer", TargetId = null, Number = "3", Heading = "Analyze Installer",
            Body = "The tool extracts the package with its bundled 7-Zip and reads the manifest — packages, "
                   + "scheduled tasks and display-driver components. Wait for it to finish before moving on.",
        },
        new TourStep
        {
            NavKey = "Customize Installation", TargetId = "customize-presets", Number = "4", Heading = "Customize Installation",
            Body = "The checklist. Required items are locked on. Start from a preset: 'Optimal Performance' "
                   + "drops ReLive, AMD Link, the User Experience Program and update tasks while keeping "
                   + "display, audio and FreeSync / Anti-Lag. Then hand-tune any row and expand "
                   + "'Why this recommendation?' for the evidence and source.",
        },
        new TourStep
        {
            NavKey = "Review and Prepare", TargetId = null, Number = "5", Heading = "Review and Prepare",
            Body = "See the final included / excluded list, then 'Prepare' rebuilds the installer folder "
                   + "with your choices. This is the last step before anything touches your system.",
        },
        new TourStep
        {
            NavKey = "Choose Services & Tweaks", TargetId = null, Number = "6", Heading = "Choose Services & Tweaks",
            Body = "Pick which AMD services and scheduled tasks to disable after install (disabled, not "
                   + "deleted). Optionally tick the curated, documented GPU registry tweaks — leave them "
                   + "unticked if you just want a clean install.",
        },
        new TourStep
        {
            NavKey = "Install and Verify", TargetId = "install-launch", Number = "7", Heading = "Install and Verify",
            Body = "'Launch AMD Installer' starts AMD's setup — click through it normally. When it finishes "
                   + "the tool force-closes the installer and Radeon Software, verifies the driver and "
                   + "applies your choices. Progress opens in its own Installation Monitor window. Restart "
                   + "afterwards.",
        },
        new TourStep
        {
            NavKey = "Post-Install", TargetId = null, Number = "8", Heading = "Post-Install (any time)",
            Body = "Not part of the install flow — open this whenever you want to trim AMD's footprint: "
                   + "host processes, scheduled tasks, system services, installed entries and leftover logs. "
                   + "Changes here apply immediately. That's the tour — press Done.",
        },
    };

    public static IReadOnlyList<TourStep> Adrenaline() => new List<TourStep>
    {
        new TourStep
        {
            NavKey = "System", TargetId = null, Number = "1", Heading = "Better Adrenaline",
            Body = "AMD controls through ADLX — it reads the settings implemented in this app, shows "
                   + "the current value next to a curated recommendation, and writes changes to the driver "
                   + "immediately. The System tab is read-only: it just confirms the tool is talking to "
                   + "your card.",
        },
        new TourStep
        {
            NavKey = "System", TargetId = "adren-profile", Number = "2", Heading = "Recommendation profile",
            Body = "This dropdown (with Re-detect / Revert all beside it) sits above every section. Pick "
                   + "Balanced / Esports / Quality / Efficiency and the tool applies that profile's "
                   + "recommended value to every row at once. 'Revert all' restores the snapshot taken when "
                   + "the section opened; 'Re-detect' re-reads the driver.",
        },
        new TourStep
        {
            NavKey = "Display", TargetId = null, Number = "3", Heading = "Display",
            Body = "Per-display settings — FreeSync, Virtual Super Resolution, GPU scaling and scaling "
                   + "mode, integer scaling, Vari-Bright. Toggles and dropdowns write live. Colour depth, "
                   + "pixel format and HDCP are shown but read-only, because changing them can black out "
                   + "the display.",
        },
        new TourStep
        {
            NavKey = "Performance", TargetId = null, Number = "4", Heading = "Performance",
            Body = "The global 3D settings. A green 'Recommended' / 'Suggested' chip means there's advice "
                   + "for that row — hover it for the reasoning and source. A small dot means you're "
                   + "already at the recommended value. Toggles apply instantly; sliders wait about a "
                   + "quarter second after you stop dragging.",
        },
        new TourStep
        {
            NavKey = "Overclocking", TargetId = "oc-mode", Number = "5", Heading = "Overclocking — pick a mode",
            Body = "This section is a short wizard. Choose Tuning Control: Default restores stock; "
                   + "Automatic lets Radeon find the numbers (Undervolt GPU / Overclock GPU / Overclock "
                   + "VRAM, or a Quiet / Balanced preset); Manual walks you through GPU clocks → VRAM → fan "
                   + "curve → power limit → review. Press Next in the section to move.",
        },
        new TourStep
        {
            NavKey = "Overclocking", TargetId = null, Number = "6", Heading = "The 15-second safety net",
            Body = "The first risky change in a session starts a 'Keep these changes?' bar. If you don't "
                   + "press Keep within 15 seconds it reverts to the values from when the section opened. "
                   + "The Review step also has 'Reset GPU tuning to default' at any time — nothing here is "
                   + "permanent until you confirm. That's the tour — press Done.",
        },
    };
}
