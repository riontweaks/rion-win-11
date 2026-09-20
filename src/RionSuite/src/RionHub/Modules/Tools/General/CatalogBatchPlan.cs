using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Modules.Tools.General;

public sealed class CatalogBatchEntry
{
    public required string GroupId { get; init; }
    public required SystemTweak Tweak { get; init; }
    public string Exclusion { get; init; } = "";
    public bool RequiresOptIn { get; init; }
    public bool Included { get; set; }
    public bool Available => Exclusion.Length == 0;
}

/// <summary>A fresh review per run; no selections or writes during construction.</summary>
public static class CatalogBatchPlan
{
    public static bool Experimental(SystemTweak tweak) => tweak.Experimental || tweak.Grade == TweakGrade.C;

    public static IReadOnlyList<CatalogBatchEntry> Create(IEnumerable<TweakBundle> bundles)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<CatalogBatchEntry>();
        foreach (var bundle in bundles)
        foreach (var tweak in bundle.Members)
        {
            if (!seen.Add(tweak.Id)) continue;
            bool optIn = Experimental(tweak) || tweak.SecuritySensitive;
            string exclusion = !string.IsNullOrWhiteSpace(tweak.ApplyBlockedReason) ? tweak.ApplyBlockedReason
                : tweak.IndividualApplyOnly || tweak.IsAdjustable || tweak.Id.StartsWith("mmcss-", StringComparison.OrdinalIgnoreCase)
                    || (tweak.ManualOnly && !optIn) ? "Individual choice; unchanged by Apply all." : "";
            entries.Add(new() { GroupId = bundle.Id, Tweak = tweak, Exclusion = exclusion,
                RequiresOptIn = optIn, Included = exclusion.Length == 0 && !optIn });
        }
        return entries;
    }

    public static async Task RunAsync(IReadOnlyList<CatalogBatchEntry> plan,
        Func<IReadOnlyList<SystemTweak>, Task<IReadOnlyList<TweakOperationResult>>> execute,
        Action<TweakOperationResult> report)
    {
        foreach (var group in plan.DistinctBy(e => e.Tweak.Id, StringComparer.OrdinalIgnoreCase).GroupBy(e => e.GroupId))
        {
            var selected = group.Where(e => e.Available && e.Included).ToList();
            foreach (var entry in group.Except(selected))
                report(TweakOperationResult.NotRun(entry.Tweak.Id, entry.Tweak.Title,
                    !string.IsNullOrEmpty(entry.Tweak.ApplyBlockedReason) ? TweakOperationStatus.Unavailable : TweakOperationStatus.Excluded,
                    entry.Available ? "Not included in this review; unchanged." : entry.Exclusion));
            if (selected.Count == 0) continue;
            IReadOnlyList<TweakOperationResult> results;
            try { results = await execute(selected.Select(e => e.Tweak).ToList()); }
            catch (Exception ex) { results = selected.Select(e => TweakOperationResult.ForAction(e.Tweak.Id, e.Tweak.Title, false, ex.Message)).ToList(); }
            bool failed = results.Any(r => !r.Success);
            foreach (var entry in selected)
            {
                var matches = results.Where(r => r.Id == entry.Tweak.Id).ToList();
                var result = matches.FirstOrDefault(r => !r.Success) ?? matches.FirstOrDefault();
                if (result == null)
                    result = TweakOperationResult.NotRun(entry.Tweak.Id, entry.Tweak.Title, TweakOperationStatus.Unavailable,
                        "Not attempted because another setting on this card failed preflight or execution.");
                else if (failed && result.Status == TweakOperationStatus.Applied)
                    result = TweakOperationResult.ForAction(entry.Tweak.Id, entry.Tweak.Title, false,
                        "The card failed and recovery was attempted. Inspect saved history before retrying.");
                report(result);
            }
            // Keep recovery diagnostics even when the same setting already has an outcome.
            foreach (var recovery in results.Where(r => r.Message?.StartsWith("Recovery needs attention:") == true)) report(recovery);
        }
    }
}
