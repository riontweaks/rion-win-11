using System;
using System.Collections.Generic;
using System.Linq;
using RadeonSoftwareSlimmer.Models;

namespace NvidiaDriverTool.Services
{
    public enum NvidiaPackagePreset { KeepAll, WithoutCompanionApp, DriverFocused }

    public static class NvidiaPackagePresets
    {
        // Exact names only. New/unknown vendor packages are retained for manual review.
        private static readonly HashSet<string> Companion = new(StringComparer.OrdinalIgnoreCase)
        { "Display.NvApp", "NvApp", "Display.GFExperience", "GFExperience", "GFExperience.NvStreamSrv", "NvStreamSrv" };
        private static readonly HashSet<string> Extras = new(StringComparer.OrdinalIgnoreCase)
        { "Display.PhysX", "HDAudio.Driver", "USBC", "Display.NVWMI", "FrameViewSDK", "Display.3DVision", "Display.NVIRUSB", "Display.Update" };

        public static HashSet<string> Exclusions(IReadOnlyList<NvidiaComponentManifest.NvidiaComponent> components, NvidiaPackagePreset preset)
        {
            if (!Enum.IsDefined(typeof(NvidiaPackagePreset), preset)) throw new ArgumentOutOfRangeException(nameof(preset));
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (preset == NvidiaPackagePreset.KeepAll) return excluded;
            foreach (var c in components)
                if (NvidiaComponentManifest.ToClassification(c) != Classification.Required
                    && (Companion.Contains(c.Name) || (preset == NvidiaPackagePreset.DriverFocused && Extras.Contains(c.Name))))
                    excluded.Add(c.Name);
            // Dependencies win over the preset, including indirect and cyclic dependencies.
            bool changed;
            do
            {
                changed = false;
                foreach (var c in components.Where(c => !excluded.Contains(c.Name)).ToArray())
                    foreach (string dependency in c.Requires) changed |= excluded.Remove(dependency);
            } while (changed);
            NvidiaInstallPlan.Arguments(components, excluded);
            return excluded;
        }
    }
}
