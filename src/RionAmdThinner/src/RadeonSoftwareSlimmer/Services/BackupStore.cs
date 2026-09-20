using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;
using RadeonSoftwareSlimmer.Intefaces;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    public enum BackupEntryKind
    {
        Registry,
        ServiceStartMode,
        ScheduledTask,
        StartupEntry,
    }

    public sealed class BackupEntry
    {
        public BackupEntryKind Kind { get; set; }
        public string Target { get; set; }
        public string Detail { get; set; }
        public bool Existed { get; set; }
        public string PreviousData { get; set; }
        public int PreviousValueKind { get; set; }
        public RegistryValueSnapshot Before { get; set; }
        public RegistryValueSnapshot Requested { get; set; }
        public RegistryValueSnapshot Verified { get; set; }
        public string HistoryStatus { get; set; }
        public string HistoryError { get; set; }
    }

    public sealed class BackupSession
    {
        private readonly List<BackupEntry> _entries = new List<BackupEntry>();
        private string _savedPath;
        private readonly DateTime _timestampUtc = DateTime.UtcNow;
        private readonly string _backupDirectory;

        public BackupSession(string backupDirectory = null)
        {
            _backupDirectory = backupDirectory ?? BackupStore.BackupDirectory;
        }

        public string Description { get; set; } = "Apply changes";

        public bool IsEmpty => _entries.Count == 0;

        public void CaptureRaw(BackupEntryKind kind, string target, string detail, bool existed, string previousData)
        {
            _entries.Add(new BackupEntry
            {
                Kind = kind,
                Target = target,
                Detail = detail,
                Existed = existed,
                PreviousData = previousData,
            });
        }

        /// <summary>Captures a registry value (with its kind) so <see cref="BackupStore.RevertLatestRegistry"/> can restore it exactly.</summary>
        public void CaptureRegistry(string target, string valueName, bool existed, object previousValue, RegistryValueKind kind)
        {
            if (_entries.Any(e => e.Kind == BackupEntryKind.Registry && string.Equals(e.Target, target, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Detail, valueName, StringComparison.OrdinalIgnoreCase))) return;
            var snapshot = RegistryValueSnapshot.Capture(existed, previousValue, kind);
            _entries.Add(new BackupEntry {
                Kind = BackupEntryKind.Registry, Target = target, Detail = valueName, Existed = existed,
                PreviousData = snapshot.Data ?? (snapshot.Strings == null ? null : JsonConvert.SerializeObject(snapshot.Strings)), PreviousValueKind = (int)kind, Before = snapshot
            });
        }

        public void WriteRegistry(IRegistryKey key, string target, string name, bool exists, object value, RegistryValueKind kind)
        {
            lock (RegistryChangeHistory.Sync)
            {
                var current = RegistryValueSnapshot.Read(key, name);
                var entry = _entries.FirstOrDefault(e => e.Kind == BackupEntryKind.Registry && string.Equals(e.Target, target, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Detail, name, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    entry = new BackupEntry { Kind = BackupEntryKind.Registry, Target = target, Detail = name,
                        Existed = current.Exists, PreviousValueKind = (int)current.Kind, PreviousData = current.Data ?? (current.Strings == null ? null : JsonConvert.SerializeObject(current.Strings)), Before = current };
                    _entries.Add(entry);
                }
                else if (entry.Verified != null && !current.Same(entry.Verified))
                    throw new IOException("This value changed outside the recorded session. Start a new change session after reviewing its current state.");
                entry.Requested = RegistryValueSnapshot.Capture(exists, value, kind);
                entry.Verified = null; entry.HistoryStatus = "Pending"; entry.HistoryError = null;
                Save(); // Must succeed before mutation.
                try
                {
                    if (!RegistryValueSnapshot.Read(key, name).Same(current)) throw new IOException("The registry value changed while preparing this action. No value was changed.");
                    if (exists) key.SetValue(name, entry.Requested.Value(), kind);
                    else key.DeleteValue(name, false);
                    entry.Verified = RegistryValueSnapshot.Read(key, name);
                    if (!entry.Verified.Same(entry.Requested)) throw new IOException("Registry readback did not match the requested value.");
                    entry.HistoryStatus = "Applied";
                    Save();
                }
                catch (Exception ex)
                {
                    entry.HistoryStatus = "Failed"; entry.HistoryError = ex.Message;
                    try { entry.Verified = RegistryValueSnapshot.Read(key, name); Save(); } catch { }
                    throw;
                }
            }
        }
        public string Save()
        {
            if (IsEmpty)
                return null;

            Directory.CreateDirectory(_backupDirectory);
            string path = _savedPath ?? Path.Combine(
                _backupDirectory,
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + ".json");

            var doc = new BackupDocument
            {
                Description = Description,
                TimestampUtc = _timestampUtc,
                SchemaVersion = 2, Machine = Environment.MachineName, User = Environment.UserDomainName + "\\" + Environment.UserName, RegistryView = Environment.Is64BitProcess ? 64 : 32,
                Entries = _entries,
            };
            RegistryChangeHistory.Persist(path, doc);
            _savedPath = path;
            return path;
        }
    }

    public sealed class BackupDocument
    {
        public int SchemaVersion { get; set; }
        public string Machine { get; set; }
        public string User { get; set; }
        public int RegistryView { get; set; }
        public string Description { get; set; }
        public DateTime TimestampUtc { get; set; }
        public List<BackupEntry> Entries { get; set; } = new List<BackupEntry>();
    }

    public static class BackupStore
    {
        public static string BackupDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RionAmdThinner", "backups");

        public static BackupDocument LatestOrNull()
        {
            if (!Directory.Exists(BackupDirectory))
                return null;

            string newest = Directory.GetFiles(BackupDirectory, "*.json")
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (newest == null)
                return null;

            try
            {
                return JsonConvert.DeserializeObject<BackupDocument>(File.ReadAllText(newest));
            }
            catch (Exception ex)
            {
                StaticViewModel.AddLogMessage(ex, "Could not read backup " + newest);
                return null;
            }
        }

        /// <summary>Legacy backups lack an expected applied value and require the existing explicit review prompt.</summary>
        public static bool LatestRequiresLegacyRestoreReview => LatestOrNull() is BackupDocument doc && doc.SchemaVersion < 2;

        public static int RevertLatestRegistry(IRegistry registry) => RevertLatestRegistry(registry, BackupDirectory);

        internal static int RevertLatestRegistry(IRegistry registry, string directory)
        {
            if (!Directory.Exists(directory)) return 0;
            string path = Directory.GetFiles(directory, "*.json").OrderByDescending(f => f).FirstOrDefault();
            if (path == null) return 0;
            BackupDocument doc;
            try { doc = JsonConvert.DeserializeObject<BackupDocument>(File.ReadAllText(path)); }
            catch (Exception ex) { StaticViewModel.AddLogMessage(ex, "Could not read latest backup"); return 0; }
            if (doc?.Entries == null) return 0;
            if (doc.SchemaVersion >= 2)
            {
                string prefix = Path.GetFileName(path) + ":";
                return RegistryChangeHistory.ListEntries(directory, registry)
                    .Count(e => e.Id.StartsWith(prefix, StringComparison.Ordinal) && e.CanRestore && RegistryChangeHistory.Restore(e.Id, registry, directory).Success);
            }
            // Compatibility path only: the existing Auto-Optimize action explicitly warns that
            // these records cannot detect external changes. The new history page never calls it.
            int restored = 0;
            foreach (var entry in doc.Entries.Where(e => e.Kind == BackupEntryKind.Registry))
            {
                try { RestoreLegacyEntry(registry, entry); restored++; }
                catch (Exception ex) { StaticViewModel.AddLogMessage(ex, "Skipped unsupported or failed legacy restore: " + entry.Target + "\\" + entry.Detail); }
            }
            return restored;
        }

        private static void RestoreLegacyEntry(IRegistry registry, BackupEntry entry)
        {
            IRegistryKey root;
            if (entry.Target.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase)) root = registry.LocalMachine;
            else if (entry.Target.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) root = registry.CurrentUser;
            else throw new InvalidDataException("Unsupported legacy registry hive.");
            var kind = (RegistryValueKind)entry.PreviousValueKind;
            object data = null;
            if (entry.Existed)
            {
                switch (kind)
                {
                    case RegistryValueKind.DWord:
                        long dword = long.Parse(entry.PreviousData, CultureInfo.InvariantCulture);
                        if (dword < int.MinValue || dword > uint.MaxValue) throw new InvalidDataException("Invalid legacy DWORD.");
                        data = unchecked((int)dword); break;
                    case RegistryValueKind.QWord: data = long.Parse(entry.PreviousData, CultureInfo.InvariantCulture); break;
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        data = entry.PreviousData ?? throw new InvalidDataException("Missing legacy string data."); break;
                    case RegistryValueKind.Binary:
                    case RegistryValueKind.None:
                        if (entry.PreviousData == null || !entry.PreviousData.StartsWith("base64:", StringComparison.Ordinal)) throw new InvalidDataException("Legacy binary bytes were not recorded.");
                        data = Convert.FromBase64String(entry.PreviousData.Substring(7)); break;
                    default: throw new InvalidDataException("Legacy value type was not preserved and cannot be reconstructed.");
                }
            }
            string keyPath = entry.Target.Substring(5);
            using (var key = root.OpenSubKey(keyPath, true) ?? (entry.Existed ? root.CreateSubKey(keyPath) : null))
            {
                if (entry.Existed) key.SetValue(entry.Detail, data, kind);
                else key?.DeleteValue(entry.Detail, false);
                if (!RegistryValueSnapshot.Read(key, entry.Detail).Same(RegistryValueSnapshot.Capture(entry.Existed, data, kind)))
                    throw new IOException("Legacy restoration could not be verified.");
            }
        }
    }
}

