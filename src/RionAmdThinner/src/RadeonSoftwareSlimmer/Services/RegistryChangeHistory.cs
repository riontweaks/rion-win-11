using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;
using RadeonSoftwareSlimmer.Intefaces;

namespace RadeonSoftwareSlimmer.Services
{
    public sealed class RegistryValueSnapshot
    {
        public bool Exists { get; set; }
        public RegistryValueKind Kind { get; set; }
        public string Data { get; set; }
        public string[] Strings { get; set; }

        public static RegistryValueSnapshot Capture(bool exists, object value, RegistryValueKind kind)
        {
            var result = new RegistryValueSnapshot { Exists = exists, Kind = exists ? kind : RegistryValueKind.Unknown };
            if (!exists) return result;
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    result.Data = unchecked((uint)Convert.ToInt64(value, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture); break;
                case RegistryValueKind.QWord:
                    result.Data = value is ulong unsigned ? unsigned.ToString(CultureInfo.InvariantCulture) : unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture); break;
                case RegistryValueKind.Binary:
                case RegistryValueKind.None:
                    result.Data = Convert.ToBase64String((byte[])value); break;
                case RegistryValueKind.MultiString:
                    result.Strings = ((string[])value).ToArray(); break;
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    result.Data = (string)value; break;
                default: throw new InvalidDataException("Unsupported registry value type; no change was made.");
            }
            return result;
        }

        internal static RegistryValueSnapshot Read(IRegistryKey key, string name)
        {
            bool exists = key != null && key.GetValueNames().Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (!exists) return Capture(false, null, RegistryValueKind.Unknown);
            object value = key is WindowsRegistryKey windows ? windows.GetRawValue(name) : key.GetValue(name, null);
            return Capture(true, value, key.GetValueKind(name));
        }

        internal object Value()
        {
            switch (Kind)
            {
                case RegistryValueKind.DWord: return unchecked((int)uint.Parse(Data, CultureInfo.InvariantCulture));
                case RegistryValueKind.QWord: return unchecked((long)ulong.Parse(Data, CultureInfo.InvariantCulture));
                case RegistryValueKind.Binary:
                case RegistryValueKind.None: return Convert.FromBase64String(Data);
                case RegistryValueKind.MultiString: return Strings.ToArray();
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString: return Data;
                default: throw new InvalidDataException("Unsupported registry snapshot type.");
            }
        }

        internal bool Same(RegistryValueSnapshot other) => other != null && Exists == other.Exists &&
            (!Exists || Kind == other.Kind && Data == other.Data &&
             (Strings == null ? other.Strings == null : other.Strings != null && Strings.SequenceEqual(other.Strings)));

        internal string Display() => !Exists ? "(absent)" : Kind + ": " +
            (Kind == RegistryValueKind.MultiString ? JsonConvert.SerializeObject(Strings) : Data);
    }

    public sealed class RegistryHistoryEntry
    {
        public string Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Description { get; set; }
        public string Target { get; set; }
        public string PreviousValue { get; set; }
        public string RequestedValue { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }
        public bool CanRestore { get; set; }
    }

    public sealed class RegistryRestoreResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public static class RegistryChangeHistory
    {
        internal static readonly object Sync = new object();
        public static IReadOnlyList<RegistryHistoryEntry> ListEntries() => ListEntries(BackupStore.BackupDirectory, new WindowsRegistry());
        public static RegistryRestoreResult Restore(string id) => Restore(id, new WindowsRegistry(), BackupStore.BackupDirectory);

        internal static IReadOnlyList<RegistryHistoryEntry> ListEntries(string directory, IRegistry registry = null)
        {
            lock (Sync)
            {
                var list = new List<RegistryHistoryEntry>();
                if (!Directory.Exists(directory)) return list;
                foreach (string path in Directory.GetFiles(directory, "*.json"))
                {
                    try
                    {
                        var doc = JsonConvert.DeserializeObject<BackupDocument>(File.ReadAllText(path));
                        if (doc?.Entries == null) throw new InvalidDataException("No backup entries.");
                        for (int i = 0; i < doc.Entries.Count; i++)
                        {
                            var entry = doc.Entries[i];
                            bool interrupted = entry.Verified == null && entry.Requested != null;
                            bool canRestore = InScope(doc) && Recoverable(entry) && (!interrupted || registry != null && CurrentMatches(registry, entry, entry.Requested));
                            list.Add(new RegistryHistoryEntry {
                                Id = Path.GetFileName(path) + ":" + i,
                                TimestampUtc = doc.TimestampUtc, Description = doc.Description,
                                Target = entry.Target + "\\" + entry.Detail,
                                PreviousValue = entry.Before?.Display() ?? entry.PreviousData ?? (entry.Existed ? "(unknown)" : "(absent)"),
                                RequestedValue = entry.Requested?.Display() ?? "(not recorded)",
                                Status = interrupted && canRestore ? "Recovery available — interrupted/unconfirmed" : entry.HistoryStatus ?? "Legacy — unverified",
                                Error = entry.HistoryError ?? "", CanRestore = canRestore
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        list.Add(new RegistryHistoryEntry { Id = Path.GetFileName(path), Description = "Unreadable backup", Target = Path.GetFileName(path), Status = "Unreadable", Error = ex.Message });
                    }
                }
                return list.OrderByDescending(e => e.TimestampUtc).ToList();
            }
        }

        internal static RegistryRestoreResult RestoreLatest(string target, IRegistry registry, string directory)
        {
            lock (Sync)
            {
                var latest = ListEntries(directory, registry).FirstOrDefault(e =>
                    string.Equals(e.Target, target, StringComparison.OrdinalIgnoreCase) && e.Status != "Restored");
                if (latest == null) return new RegistryRestoreResult { Message = "No saved original exists for this setting. No default was substituted; use the setting’s Revert changes action to review older records." };
                if (!latest.CanRestore) return new RegistryRestoreResult { Message = "The latest record cannot be restored automatically: " + latest.Status + ". Review the setting’s Revert changes action." };
                return Restore(latest.Id, registry, directory);
            }
        }

        private static bool CurrentMatches(IRegistry registry, BackupEntry entry, RegistryValueSnapshot expected)
        {
            try
            {
                IRegistryKey root;
                if (entry.Target.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase)) root = registry.LocalMachine;
                else if (entry.Target.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) root = registry.CurrentUser;
                else return false;
                using (var key = root.OpenSubKey(entry.Target.Substring(5), false)) return RegistryValueSnapshot.Read(key, entry.Detail).Same(expected);
            }
            catch { return false; }
        }

        private static bool InScope(BackupDocument doc) => doc.SchemaVersion == 2 &&
            doc.Machine == Environment.MachineName && doc.User == Environment.UserDomainName + "\\" + Environment.UserName &&
            doc.RegistryView == (Environment.Is64BitProcess ? 64 : 32);

        private static bool Recoverable(BackupEntry entry) => entry.Kind == BackupEntryKind.Registry && entry.Before != null &&
            entry.Requested != null && (entry.Verified == null || entry.Verified.Same(entry.Requested)) &&
            (entry.HistoryStatus == "Pending" || entry.HistoryStatus == "Applied" || entry.HistoryStatus == "Failed" || entry.HistoryStatus == "Conflict" || entry.HistoryStatus == "Restoring" || entry.HistoryStatus == "Restore failed");

        internal static RegistryRestoreResult Restore(string id, IRegistry registry, string directory)
        {
            lock (Sync)
            {
                BackupDocument doc = null; BackupEntry entry = null; string path = null;
                try
                {
                    int separator = id?.LastIndexOf(':') ?? -1;
                    if (separator < 1 || !int.TryParse(id.Substring(separator + 1), out int index) || index < 0) throw new InvalidDataException("Invalid history entry.");
                    string name = id.Substring(0, separator);
                    if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid history entry.");
                    path = Path.Combine(directory, name);
                    doc = JsonConvert.DeserializeObject<BackupDocument>(File.ReadAllText(path));
                    entry = doc.Entries[index];
                    if (!InScope(doc) || !Recoverable(entry)) return new RegistryRestoreResult { Message = "This entry has no verified expected state or was already restored. No registry value was changed." };
                    IRegistryKey root;
                    if (entry.Target.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase)) root = registry.LocalMachine;
                    else if (entry.Target.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) root = registry.CurrentUser;
                    else throw new InvalidDataException("Unsupported registry hive.");
                    string keyPath = entry.Target.Substring(5);
                    using (var currentKey = root.OpenSubKey(keyPath, false))
                    {
                        var current = RegistryValueSnapshot.Read(currentKey, entry.Detail);
                        if ((entry.HistoryStatus == "Restoring" || entry.HistoryStatus == "Restore failed") && current.Same(entry.Before))
                        {
                            entry.HistoryStatus = "Restored"; entry.HistoryError = null; Persist(path, doc);
                            return new RegistryRestoreResult { Success = true, Message = "The interrupted restore was already applied and is now verified." };
                        }
                        if (!current.Same(entry.Verified ?? entry.Requested))
                        {
                            entry.HistoryStatus = "Conflict"; entry.HistoryError = "The current value differs from the recorded applied state. No value was changed."; Persist(path, doc);
                            return new RegistryRestoreResult { Message = entry.HistoryError };
                        }
                    }
                    if (entry.Before.Exists) entry.Before.Value(); // Validate typed data before any mutation.
                    // Durable intent before mutation. Retain the known after-state for safe retry.
                    entry.HistoryStatus = "Restoring"; entry.HistoryError = null; Persist(path, doc);
                    using (var key = root.OpenSubKey(keyPath, true) ?? (entry.Before.Exists ? root.CreateSubKey(keyPath) : null))
                    {
                        if (!RegistryValueSnapshot.Read(key, entry.Detail).Same(entry.Verified ?? entry.Requested)) throw new IOException("The registry value changed while preparing restore. No value was changed.");
                        if (entry.Before.Exists) key.SetValue(entry.Detail, entry.Before.Value(), entry.Before.Kind);
                        else key?.DeleteValue(entry.Detail, false);
                        if (!RegistryValueSnapshot.Read(key, entry.Detail).Same(entry.Before)) throw new IOException("Restore readback did not match the original value.");
                    }
                    entry.HistoryStatus = "Restored"; entry.HistoryError = null; Persist(path, doc);
                    return new RegistryRestoreResult { Success = true, Message = "Restored the exact previous registry value and verified it." };
                }
                catch (Exception ex)
                {
                    if (entry != null && doc != null && path != null)
                    {
                        entry.HistoryStatus = "Restore failed"; entry.HistoryError = ex.Message;
                        try { Persist(path, doc); } catch { }
                    }
                    return new RegistryRestoreResult { Message = "Restore failed: " + ex.Message };
                }
            }
        }

        internal static void Persist(string path, BackupDocument doc)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 1024, true))
                    { writer.Write(JsonConvert.SerializeObject(doc, Formatting.Indented)); writer.Flush(); }
                    stream.Flush(true);
                }
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static void Write(BackupSession backup, IRegistryKey key, string target, string name, bool exists, object value, RegistryValueKind kind)
        {
            (backup ?? new BackupSession { Description = "Registry change" }).WriteRegistry(key, target, name, exists, value, kind);
        }
    }
}



