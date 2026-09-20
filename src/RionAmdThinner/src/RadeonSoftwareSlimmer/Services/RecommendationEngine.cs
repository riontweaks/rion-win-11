using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Rule-driven, evidence-graded recommendation lookup. Rules match known identifiers
    /// (package product name, service name, task name) — never broad folder names alone.
    /// Anything not matched falls back to Classification.Unknown / keep by default.
    /// Rules load from %LOCALAPPDATA%\AmdDriverManager\recommendations.json if present,
    /// otherwise the built-in seed set is used. Remote/rule updates are never auto-applied.
    /// </summary>
    public sealed class RecommendationEngine
    {
        private readonly RecommendationDatabase _db;

        public RecommendationEngine()
        {
            _db = LoadOverride() ?? BuiltInSeed();
        }

        public string RulesVersion => _db.Version;
        public IReadOnlyList<RecommendationRecord> AllRules => _db.Records;

        public RecommendationRecord Match(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return null;

            return _db.Records.FirstOrDefault(r =>
                r.ApplicableIdentifiers.Any(id =>
                    identifier.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0
                    || id.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public Classification Classify(string identifier) =>
            Match(identifier)?.Recommendation ?? Classification.Unknown;

        /// <summary>
        /// Whether a live, already-installed service or scheduled task with this identifier
        /// should be turned off automatically after a verified install. Only Optional /
        /// CandidateToDisable items qualify, and never those a rule marks
        /// <see cref="ComponentAction.ReviewAfterInstall"/>. Required, KeepByDefault and
        /// unmatched (Unknown) identifiers always return false — nothing is disabled just
        /// for being present.
        /// </summary>
        public bool ShouldAutoDisable(string identifier)
        {
            RecommendationRecord rec = Match(identifier);
            if (rec == null)
                return false;
            if (rec.DefaultAction == ComponentAction.ReviewAfterInstall)
                return false;

            return rec.Recommendation == Classification.Optional
                || rec.Recommendation == Classification.CandidateToDisable;
        }

        private static RecommendationDatabase LoadOverride()
        {
            try
            {
                string path = Path.Combine(AuditLog.Root, "recommendations.json");
                if (!File.Exists(path)) return null;
                var db = JsonConvert.DeserializeObject<RecommendationDatabase>(File.ReadAllText(path));
                StaticViewModel.AddLogMessage($"Loaded recommendation rules v{db?.Version} from {path}");
                return db;
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Falling back to built-in recommendation rules");
                return null;
            }
        }

        private static RecommendationDatabase BuiltInSeed()
        {
            return new RecommendationDatabase
            {
                Version = "seed-2026.09",
                GeneratedUtc = DateTime.UtcNow,
                Records = new List<RecommendationRecord>
                {
                    Rec("core-display", new[] { "Radeon Software", "Display Driver", "WT6A_INF", "amdkmdag", "amdwddmg" },
                        Classification.Required, ComponentAction.Keep, EvidenceGrade.A_Official,
                        "The core AMD display driver (kernel-mode driver, DirectX/Vulkan/OpenGL user-mode drivers).",
                        featureLoss: "Removing this leaves the GPU on the Microsoft Basic Display Adapter.",
                        source: "AMD driver package metadata / Microsoft WDDM documentation"),

                    Rec("hd-audio", new[] { "AtiHDAudioService", "HD Audio", "HDMI Audio" },
                        Classification.KeepByDefault, ComponentAction.Keep, EvidenceGrade.C_CommunityInformed,
                        "HDMI / DisplayPort audio output from the GPU.",
                        featureLoss: "No audio over HDMI/DisplayPort. Keep unless you never use display audio.",
                        source: "RadeonSoftwareSlimmer wiki — System Services and Drivers"),

                    Rec("external-events", new[] { "AMD External Events Utility", "atiesrxx" },
                        Classification.KeepByDefault, ComponentAction.Keep, EvidenceGrade.C_CommunityInformed,
                        "Monitors the system for FreeSync, Chill, FRTC and Anti-Lag activation.",
                        featureLoss: "FreeSync / Chill / Anti-Lag may stop working. Keep by default.",
                        source: "RadeonSoftwareSlimmer wiki — System Services and Drivers"),

                    Rec("crash-defender", new[] { "AMD Crash Defender", "amdfendr", "amdfendrmgr", "amdlog" },
                        Classification.KeepByDefault, ComponentAction.Keep, EvidenceGrade.C_CommunityInformed,
                        "Captures crash dumps for AMD troubleshooting.",
                        benefit: "Slightly less background activity if disabled.",
                        featureLoss: "Less diagnostic information if the driver crashes.",
                        source: "RadeonSoftwareSlimmer wiki — System Services and Drivers"),

                    Rec("amd-link", new[] { "AMD Link", "AMDXE", "AMDSAFD", "AMDRSSrcExt" },
                        Classification.Optional, ComponentAction.Keep, EvidenceGrade.C_CommunityInformed,
                        "AMD Link remote streaming, its controller emulation and streaming-audio driver.",
                        benefit: "Removes AMD Link streaming components and their background pieces.",
                        featureLoss: "AMD Link phone/PC streaming and Noise Suppression will not be available.",
                        source: "RadeonSoftwareSlimmer wiki — Installer Packages"),

                    Rec("relive-recording", new[] { "ReLive", "Recording", "Streaming", "DVR", "AMD DVR" },
                        Classification.Optional, ComponentAction.Keep, EvidenceGrade.C_CommunityInformed,
                        "Radeon ReLive: in-game recording, instant replay and streaming.",
                        benefit: "Removes the capture/record subsystem.",
                        featureLoss: "No in-game recording, instant replay, streaming or the capture overlay.",
                        source: "RadeonSoftwareSlimmer wiki — Installer Packages"),

                    Rec("uxp", new[] { "AUEP", "User Experience", "AUEPLauncher", "DVRAnalytics" },
                        Classification.Optional, ComponentAction.Keep, EvidenceGrade.C_CommunityInformed,
                        "AMD User Experience Program — usage-data collection.",
                        benefit: "Stops the User Experience Program telemetry uploads.",
                        featureLoss: "None visible to the user.",
                        source: "RadeonSoftwareSlimmer wiki — Scheduled Tasks"),

                    Rec("install-manager-task", new[] { "AMD Install Manager", "StartCN", "StartCNBM" },
                        Classification.Optional, ComponentAction.ReviewAfterInstall, EvidenceGrade.C_CommunityInformed,
                        "Scheduled tasks that check for and start AMD Software / driver updates.",
                        benefit: "Fewer background update checks.",
                        featureLoss: "You will need to check for driver updates manually.",
                        source: "RadeonSoftwareSlimmer wiki — Scheduled Tasks"),
                },
            };
        }

        private static RecommendationRecord Rec(string id, string[] ids, Classification cls, ComponentAction action,
            EvidenceGrade grade, string purpose, string benefit = null, string featureLoss = null, string source = null)
        {
            return new RecommendationRecord
            {
                Id = id,
                ApplicableIdentifiers = ids.ToList(),
                Recommendation = cls,
                DefaultAction = action,
                EvidenceGrade = grade,
                Confidence = grade == EvidenceGrade.A_Official ? "High" : "Medium",
                Purpose = purpose,
                BenefitOfDisabling = benefit,
                FeatureLoss = featureLoss,
                SourceTitle = source,
                SourcePublisher = source,
                TestState = grade == EvidenceGrade.A_Official ? "Officially documented" : "Community/project research",
                LastVerifiedDate = "2026-09",
                Reversible = true,
            };
        }
    }
}
