using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Models
{
    public enum ComponentType
    {
        Package,
        InstallerEntry,
        Service,
        ScheduledTask,
        StartupEntry,
        DisplayDriverComponent,
        UnknownFile,
    }

    /// <summary>A single item discovered inside an extracted AMD installer.</summary>
    public sealed class InstallerComponent
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public ComponentType ComponentType { get; set; }
        public string Version { get; set; }
        public string ManifestPath { get; set; }
        public string InstallPath { get; set; }

        public Classification Classification { get; set; } = Classification.Unknown;
        public ComponentAction DefaultAction { get; set; } = ComponentAction.Keep;
        public List<ComponentAction> AllowedActions { get; set; } = new List<ComponentAction> { ComponentAction.Keep };

        public List<string> Dependencies { get; set; } = new List<string>();
        public List<string> Conflicts { get; set; } = new List<string>();

        public string FeatureDescription { get; set; }
        public string ExclusionImpact { get; set; }
        public bool IsCoreDriverComponent { get; set; }
        public bool IsUnknown { get; set; } = true;
        public string RecommendationId { get; set; }

        public bool CanExclude =>
            !IsCoreDriverComponent &&
            Classification != Classification.Required &&
            AllowedActions.Contains(ComponentAction.Exclude);
    }
}
