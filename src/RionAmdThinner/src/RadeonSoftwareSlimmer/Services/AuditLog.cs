using System;
using System.IO;
using Newtonsoft.Json;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Structured audit trail. Every recommendation, selected change, applied action, result,
    /// error and rollback is appended as one JSON line. Also echoed to the Activity Log view.
    /// </summary>
    public static class AuditLog
    {
        public static string Root =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AmdDriverManager");

        public static string CurrentSessionId { get; private set; } = "startup";

        public static void StartSession(string sessionId)
        {
            CurrentSessionId = string.IsNullOrWhiteSpace(sessionId) ? "session" : sessionId;
            Directory.CreateDirectory(SessionDir(CurrentSessionId));
            Write(new AuditLogEntry
            {
                Operation = "SessionStart",
                Result = "OK",
                SessionId = CurrentSessionId,
                Reversible = false,
            });
        }

        public static string SessionDir(string sessionId) =>
            Path.Combine(Root, "sessions", sessionId);

        public static void Write(AuditLogEntry entry)
        {
            entry.SessionId ??= CurrentSessionId;
            entry.Timestamp = DateTime.UtcNow;

            try
            {
                string dir = SessionDir(entry.SessionId);
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "audit.jsonl"),
                    JsonConvert.SerializeObject(entry) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not write audit entry");
            }

            string summary = $"{entry.Operation}: {entry.Target}";
            if (!string.IsNullOrEmpty(entry.OldValue) || !string.IsNullOrEmpty(entry.NewValue))
                summary += $"  ({entry.OldValue} -> {entry.NewValue})";
            if (!string.IsNullOrEmpty(entry.Result))
                summary += $"  [{entry.Result}]";
            StaticViewModel.AddLogMessage(summary);
        }

        public static void Action(string userAction, string operation, string target,
            string oldValue = null, string newValue = null, string result = "OK",
            bool reversible = true, string error = null)
        {
            Write(new AuditLogEntry
            {
                UserAction = userAction,
                Operation = operation,
                Target = target,
                OldValue = oldValue,
                NewValue = newValue,
                Result = error == null ? result : "Error",
                ErrorDetails = error,
                Reversible = reversible,
            });
        }
    }
}
