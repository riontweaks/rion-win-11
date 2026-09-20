using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Modules.Tools.General;

public sealed record TweakBatchItem(string Id, string Title, Func<Task<TweakOperationResult>> Run);

public static class TweakBatch
{
    public static async Task RunAsync(IEnumerable<TweakBatchItem> items, Action<int, int, TweakOperationResult> report)
    {
        var targets = items.DistinctBy(i => i.Id).ToList();
        for (int i = 0; i < targets.Count; i++)
        {
            var item = targets[i];
            TweakOperationResult result;
            try { result = await item.Run(); }
            catch (Exception ex) { result = TweakOperationResult.ForAction(item.Id, item.Title, false, ex.Message); }
            report(i + 1, targets.Count, result);
        }
    }
}
