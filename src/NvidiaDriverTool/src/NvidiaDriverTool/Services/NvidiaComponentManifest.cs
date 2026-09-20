using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RadeonSoftwareSlimmer.Models;

namespace NvidiaDriverTool.Services
{
    /// <summary>
    /// Parses NVIDIA's <c>setup.cfg</c>, the component manifest at the root of an extracted driver
    /// package. One flat list of ~19 sub-packages:
    /// <code>
    /// &lt;install&gt;
    ///   &lt;sub-package name="Display.Driver" disposition="critical"&gt; ... &lt;/sub-package&gt;
    ///   &lt;sub-package name="Display.NvApp" disposition="default" userSelectable="false" title="${{CapitalizedNvAppTitle}}"&gt;
    ///     &lt;dependencies&gt;&lt;package type="after" package="Display.Driver"/&gt;&lt;/dependencies&gt;
    ///   &lt;/sub-package&gt;
    /// &lt;/install&gt;
    /// </code>
    /// <c>title</c> values reference <c>${{...}}</c> placeholders from the string table earlier in
    /// the file; most components have none and are shown by their raw <c>name</c>.
    /// </summary>
    public static class NvidiaComponentManifest
    {
        public sealed class NvidiaComponent
        {
            public string Name { get; set; }
            public string Title { get; set; }
            public string Disposition { get; set; }   // "critical" | "default" | "demand"
            public bool UserSelectable { get; set; } = true;
            public List<string> Requires { get; } = new();
            public List<string> Services { get; } = new();
            public List<string> Tasks { get; } = new();
        }

        /// <summary>Parses <paramref name="extractedRoot"/>\setup.cfg. Throws if the file is
        /// missing or doesn't look like an NVIDIA manifest — same "fail loudly, don't guess"
        /// stance as <see cref="NvidiaDriverDownloadService"/>.</summary>
        public static IReadOnlyList<NvidiaComponent> Parse(string extractedRoot)
        {
            string path = Path.Combine(extractedRoot, "setup.cfg");
            if (!File.Exists(path))
                throw new InvalidOperationException("setup.cfg not found — is this really an extracted NVIDIA driver package?");

            XDocument doc = XDocument.Load(path);
            var components = doc.Descendants("sub-package")
                .Select(e => new NvidiaComponent
                {
                    Name = (string)e.Attribute("name"),
                    Title = (string)e.Attribute("title"),
                    Disposition = (string)e.Attribute("disposition") ?? "default",
                    UserSelectable = (string)e.Attribute("userSelectable") != "false",
                })
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .ToList();

            if (components.Count == 0)
                throw new InvalidOperationException("setup.cfg had no <sub-package> entries — NVIDIA may have changed the manifest format.");

            foreach (string file in Directory.EnumerateFiles(extractedRoot, "*.nvi", SearchOption.AllDirectories))
            {
                var manifest = XDocument.Load(file);
                string Resolve(string value)
                {
                    if (value == null) return null;
                    var strings = doc.Descendants("string").Concat(manifest.Descendants("string"))
                        .Where(e => e.Attribute("name") != null && e.Attribute("value") != null)
                        .GroupBy(e => (string)e.Attribute("name"))
                        .ToDictionary(g => g.Key, g => (string)g.Last().Attribute("value"));
                    for (int pass = 0; pass < 8; pass++)
                    {
                        string previous = value;
                        foreach (var entry in strings) value = value.Replace("${{" + entry.Key + "}}", entry.Value);
                        if (value == previous) break;
                    }
                    return value;
                }
                string manifestName = Resolve((string)manifest.Root?.Attribute("name"));
                if (string.IsNullOrWhiteSpace(manifestName)) continue;
                if (!System.Text.RegularExpressions.Regex.IsMatch(manifestName, @"\A[A-Za-z0-9_.-]+\z"))
                    throw new InvalidOperationException("Unresolved NVIDIA package identity in " + Path.GetFileName(file));
                var component = components.FirstOrDefault(c => c.Name.Equals(manifestName, StringComparison.OrdinalIgnoreCase));
                if (component == null)
                {
                    component = new NvidiaComponent { Name = manifestName,
                        Title = Resolve((string)manifest.Root.Attribute("title")),
                        Disposition = (string)manifest.Root.Attribute("disposition") ?? "default" };
                    components.Add(component);
                }
                component.Requires.AddRange(manifest.Descendants("package")
                    .Where(e => (string)e.Attribute("type") == "requires" || (string)e.Attribute("type") == "installs")
                    .Select(e => Resolve((string)e.Attribute("package"))).Where(n => n != null));
                component.Services.AddRange(manifest.Descendants("createService")
                    .Select(e => Resolve((string)e.Attribute("name"))).Distinct());
                component.Tasks.AddRange(manifest.Descendants("scheduleTask")
                    .Where(e => (string)e.Attribute("action") == "create")
                    .Select(e => Resolve((string)e.Attribute("taskName"))).Distinct());
            }
            foreach (var entry in doc.Descendants("sub-package"))
            {
                var component = components.First(c => c.Name == (string)entry.Attribute("name"));
                component.Requires.AddRange(entry.Descendants("package")
                    .Where(e => (string)e.Attribute("type") == "requires" || (string)e.Attribute("type") == "installs")
                    .Select(e => (string)e.Attribute("package")).Where(n => n != null));
            }
            return components;
        }

        /// <summary>Maps NVIDIA's disposition to the same <see cref="Classification"/> enum the
        /// AMD component picker already uses, so <c>ComponentRowViewModel</c> (referenced, not
        /// duplicated — see the project plan) works unchanged for NVIDIA components too.
        /// Critical disposition locks packages; hidden UI status alone does not imply a required dependency.</summary>
        public static Classification ToClassification(NvidiaComponent c)
        {
            // Hidden from NVIDIA's stock component page does not mean driver-critical.
            if (string.Equals(c.Name, "Display.Driver", StringComparison.OrdinalIgnoreCase)) return Classification.Required;
            switch (c.Disposition)
            {
                case "critical": return Classification.Required;
                case "default": return Classification.KeepByDefault;
                case "demand": return Classification.Optional;
                default: return Classification.Unknown;
            }
        }

        /// <summary>Human-friendly fallback when a component has no <c>title</c> attribute (most
        /// don't) — turns "Display.PhysX" into "PhysX", "NvContainer.LocalSystem" into
        /// "NvContainer LocalSystem", etc. Good enough for a first pass; swap in curated
        /// descriptions per component the way <c>ComponentRowViewModel.Purpose</c> already
        /// supports via a real <see cref="RecommendationRecord"/> once there's time to write them.</summary>
        public static string DisplayName(NvidiaComponent c)
        {
            if (!string.IsNullOrEmpty(c.Title) && !c.Title.Contains("${{"))
                return c.Title;
            string name = c.Name.Contains(".") ? c.Name.Substring(c.Name.LastIndexOf('.') + 1) : c.Name;
            return name.Replace(".", " ");
        }
    }
}
