using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Modules.About;

public sealed class ModuleRow
{
    public string Name { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Version { get; init; } = "";
}

public sealed class InfoRow
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
}

/// <summary>
/// About page content. The "This PC" rows are read live rather than hardcoded — cheap registry
/// and Environment reads only, no WMI, so opening this tab never stalls the UI.
/// </summary>
public sealed class AboutViewModel
{
    public string AppVersion { get; }
    public string InstalledDate { get; }
    public string GpuName { get; }

    public AboutViewModel()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        AppVersion = asm.GetName().Version?.ToString(3) ?? "1.0.0";
        InstalledDate = Environment.ProcessPath is string executable && System.IO.File.Exists(executable)
            ? System.IO.File.GetCreationTime(executable).ToString("yyyy.MM.dd")
            : "Unavailable";

        GpuName = SafeGpuName();
        SystemInfo = BuildSystemInfo();
    }

    private static string SafeGpuName()=>"See driver installation";

    public IReadOnlyList<InfoRow> SystemInfo { get; }

    private List<InfoRow> BuildSystemInfo()
    {
        var rows = new List<InfoRow>();
        string? Cv(string name)
        {
            try
            {
                using RegistryKey? k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                return k?.GetValue(name)?.ToString();
            }
            catch { return null; }
        }

        string product = Cv("ProductName") ?? "Windows";
        string display = Cv("DisplayVersion") ?? "";
        // ProductName still reports "Windows 10" on 11; the build number is the honest signal.
        string build = Cv("CurrentBuildNumber") ?? Environment.OSVersion.Version.Build.ToString();
        if (int.TryParse(build, out int b) && b >= 22000 && product.Contains("Windows 10"))
            product = product.Replace("Windows 10", "Windows 11");

        rows.Add(new InfoRow { Label = "Windows", Value = string.IsNullOrEmpty(display) ? product : $"{product} {display}" });
        rows.Add(new InfoRow { Label = "Build", Value = build + "." + (Cv("UBR") ?? "0") });

        string cpu = "Unknown";
        try
        {
            using RegistryKey? k = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpu = k?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? cpu;
        }
        catch { /* leave Unknown */ }
        rows.Add(new InfoRow { Label = "Processor", Value = cpu });

        try { rows.Add(new InfoRow { Label = "Memory", Value = $"{RadeonSoftwareSlimmer.Optimize.SystemInfo.TotalPhysicalMemoryGb()} GB" }); }
        catch { /* skip the row rather than show a wrong number */ }

        rows.Add(new InfoRow { Label = "Graphics", Value = GpuName });
        rows.Add(new InfoRow { Label = "Machine", Value = Environment.MachineName });
        return rows;
    }

    public IReadOnlyList<ModuleRow> Modules { get; } = new List<ModuleRow>
    {
        new ModuleRow { Name = "AMD Driver Tool", Version = "1.12.0-fork",
            Detail = "Downloads, slims and installs an official Radeon driver package." },
        new ModuleRow { Name = "NVIDIA Driver Tool", Version = "preview",
            Detail = "Component-selective GeForce driver install. The install step is untested on hardware." },
        new ModuleRow { Name = "Power Setting Explorer+", Version = "2.4",
            Detail = "Every Windows power setting, hidden ones included, per plan." },
        new ModuleRow { Name = "Automatic GPU updates", Version = "AMD / NVIDIA",
            Detail = "Checks drivers older than six months, prepares matching signed packages and asks before installation. Gaming retains recording; eSports excludes recognized recording packages. AMD setup may require vendor prompts; OEM and hybrid PCs require review." },
        new ModuleRow { Name = "Tweaks and Fixes", Version = "Reversible",
            Detail = "Registry and system settings with individual controls, current-state checks, recommended selections and repair tools." },
        new ModuleRow { Name = "Debloat", Version = "App inventory",
            Detail = "Review installed apps and choose what to remove. Removed apps need to be reinstalled separately." },
    };
}
