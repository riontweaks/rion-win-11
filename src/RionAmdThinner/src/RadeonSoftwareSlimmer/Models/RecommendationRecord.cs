using System;
using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Models
{
    /// <summary>
    /// One versioned, evidence-graded recommendation. Loaded from a JSON data file so
    /// classifications can be improved without an app update. Grade C/D wording must stay
    /// "community-informed" / "not fully confirmed" — never "verified".
    /// </summary>
    public sealed class RecommendationRecord
    {
        public string Id { get; set; }
        public List<string> ApplicableIdentifiers { get; set; } = new List<string>();
        public string DriverVersionScope { get; set; }
        public string HardwareScope { get; set; }

        public Classification Recommendation { get; set; } = Classification.Unknown;
        public ComponentAction DefaultAction { get; set; } = ComponentAction.Keep;
        public string Confidence { get; set; } = "Low";
        public EvidenceGrade EvidenceGrade { get; set; } = EvidenceGrade.D_Unverified;

        public string Purpose { get; set; }
        public string BenefitOfDisabling { get; set; }
        public string FeatureLoss { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public bool Reversible { get; set; } = true;

        public string SourceTitle { get; set; }
        public string SourceUrl { get; set; }
        public string SourceExcerpt { get; set; }
        public string SourcePublisher { get; set; }
        public string LastVerifiedDate { get; set; }
        public string TestState { get; set; } = "Unverified";
        public string Notes { get; set; }

        public string EvidenceGradeLabel
        {
            get
            {
                switch (EvidenceGrade)
                {
                    case EvidenceGrade.A_Official: return "A — Official documentation / signed metadata";
                    case EvidenceGrade.B_ReproduciblyTested: return "B — Reproducibly tested";
                    case EvidenceGrade.C_CommunityInformed: return "C — Community-informed";
                    default: return "D — Not fully confirmed";
                }
            }
        }

        /// <summary>Confidence-appropriate lead-in for the evidence panel.</summary>
        public string WordedClaim
        {
            get
            {
                switch (EvidenceGrade)
                {
                    case EvidenceGrade.A_Official:
                        return "AMD/Microsoft documentation identifies this component as: " + Purpose;
                    case EvidenceGrade.B_ReproduciblyTested:
                        return "Reproduced in testing: " + Purpose;
                    case EvidenceGrade.C_CommunityInformed:
                        return "Community/project research associates this item with: " + Purpose;
                    default:
                        return "This item is not sufficiently verified. Keep it enabled/installed.";
                }
            }
        }
    }

    public sealed class RecommendationDatabase
    {
        public string Version { get; set; } = "0";
        public DateTime GeneratedUtc { get; set; }
        public List<RecommendationRecord> Records { get; set; } = new List<RecommendationRecord>();
    }
}
