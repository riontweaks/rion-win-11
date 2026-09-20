using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RadeonSoftwareSlimmer.Models;

namespace NvidiaDriverTool.Services
{
    /// <summary>Installer 2.0 uses -deselect for exclusions; -n suppresses reboot.
    /// Package files remain intact so NVIDIA can resolve dependencies.</summary>
    public static class NvidiaInstallPlan
    {
        public static string ExportLauncher(IReadOnlyList<NvidiaComponentManifest.NvidiaComponent> components, IEnumerable<string> exclusions, NvidiaInstallationOptions options = null) =>
            "@echo off\r\nsetlocal DisableDelayedExpansion\r\npushd \"%~dp0\"\r\nif errorlevel 1 exit /b 1\r\n\".\\setup.exe\" " + Arguments(components, exclusions, options) +
            "\r\nset \"RionExit=%errorlevel%\"\r\npopd\r\nexit /b %RionExit%\r\n";

        public static bool IsTrusted(DriverInstallerInfo info) => info != null
            && (info.SignatureStatus == SignatureStatus.Valid || info.SignatureStatus == SignatureStatus.ValidButNotAmd)
            && string.Equals(info.Publisher, "NVIDIA Corporation", StringComparison.OrdinalIgnoreCase);

        public static string Arguments(IReadOnlyList<NvidiaComponentManifest.NvidiaComponent> components,
            IEnumerable<string> exclusions, NvidiaInstallationOptions options = null)
        {
            var excluded = new HashSet<string>(exclusions, StringComparer.OrdinalIgnoreCase);
            if (!components.Any(c => c.Name.Equals("Display.Driver", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The package has no NVIDIA display driver.");
            foreach (string name in excluded)
            {
                var component = components.SingleOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (component == null || !Regex.IsMatch(name, @"\A[A-Za-z0-9_.-]+\z"))
                    throw new InvalidOperationException("Invalid package selection: " + name);
                if (NvidiaComponentManifest.ToClassification(component) == Classification.Required)
                    throw new InvalidOperationException("Cannot exclude a required package: " + name);
            }
            foreach (var component in components.Where(c => !excluded.Contains(c.Name)))
                foreach (string dependency in component.Requires)
                    if (excluded.Contains(dependency))
                        throw new InvalidOperationException(component.Name + " needs " + dependency + ". Keep the dependency or exclude its dependent package too.");
            options ??= new NvidiaInstallationOptions();
            return (options.Unattended ? "-s -n" : "-n") + (options.CleanInstall ? " -clean" : "") + string.Concat(excluded.OrderBy(n => n).Select(n => " -deselect:" + n));
        }
    }
}
