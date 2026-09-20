using System.Collections.Generic;
using System.Linq;

namespace RadeonSoftwareSlimmer.Optimize
{
    public enum TweakOperationStatus { Applied, AlreadyConfigured, Restored, Unsupported, Unavailable, Failed, Excluded }
    public sealed class TweakOperationResult
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public TweakOperationStatus Status { get; private set; }
        public string Message { get; private set; }
        public string PreviousValue { get; set; }
        public string RequestedValue { get; set; }
        public bool RebootRequired { get; set; }
        public bool Success => Status == TweakOperationStatus.Applied || Status == TweakOperationStatus.AlreadyConfigured || Status == TweakOperationStatus.Restored;
        public string Display => Title + ": " + StatusLabel + " - " + Message;
        public string StatusLabel => Status == TweakOperationStatus.AlreadyConfigured ? "Already configured" : Status.ToString();
        internal TweakOperationResult Finish(TweakOperationStatus status, string message) { Status = status; Message = message; return this; }
        public static TweakOperationResult ForAction(string id, string title, bool success, string message, bool changed = true) =>
            new TweakOperationResult { Id = id, Title = title }.Finish(success ? changed ? TweakOperationStatus.Applied : TweakOperationStatus.AlreadyConfigured : TweakOperationStatus.Failed, message);
        public static TweakOperationResult NotRun(string id, string title, TweakOperationStatus status, string message) =>
            new TweakOperationResult { Id = id, Title = title }.Finish(status, message);
        public static string Summarize(IEnumerable<TweakOperationResult> results)
        {
            var list = results.ToList();
            return string.Join("; ", new[] {
                list.Count(r => r.Status == TweakOperationStatus.Applied) + " changed",
                list.Count(r => r.Status == TweakOperationStatus.Restored) + " restored",
                list.Count(r => r.Status == TweakOperationStatus.AlreadyConfigured) + " already configured",
                list.Count(r => r.Status == TweakOperationStatus.Excluded) + " excluded",
                list.Count(r => r.Status == TweakOperationStatus.Unsupported) + " unsupported",
                list.Count(r => r.Status == TweakOperationStatus.Unavailable) + " unavailable",
                list.Count(r => r.Status == TweakOperationStatus.Failed) + " failed" });
        }
    }
}
