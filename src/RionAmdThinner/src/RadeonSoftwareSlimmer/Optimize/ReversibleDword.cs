using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Intefaces;

namespace RadeonSoftwareSlimmer.Optimize
{
    /// <summary>Persistent ownership for individually chosen policies. Never guesses the prior value.</summary>
    public sealed class ReversibleDword
    {
        private readonly IRegistryKey _root;
        private readonly string _path, _name, _backup;
        private readonly int _target;
        private readonly object _gate = new object();
        public ReversibleDword(IRegistryKey root, string path, string name, int target, string backup)
        { _root = root; _path = path; _name = name; _target = target; _backup = backup; }

        public sealed class Snapshot
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public int Target { get; set; }
            public int? Before { get; set; }
        }
        private int? Read()
        {
            using var key = _root.OpenSubKey(_path, false);
            object value = key?.GetValue(_name);
            if (value == null) return null;
            if (key.GetValueKind(_name) != RegistryValueKind.DWord || !(value is int))
                throw new InvalidOperationException("Existing policy is not a DWORD; leaving it unchanged.");
            return (int)value;
        }
        public bool? Inspect() { try { return Read() == _target; } catch { return null; } }
        public bool Apply()
        {
            lock (_gate)
            {
                int? before = Read();
                if (before == _target) return false;
                if (File.Exists(_backup))
                    throw new InvalidOperationException("A previous backup exists but the policy changed. Resolve the previous change before applying again.");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_backup));
                var snapshot = new Snapshot { Path = _path, Name = _name, Target = _target, Before = before };
                using (var stream = new FileStream(_backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { JsonSerializer.Serialize(stream, snapshot); stream.Flush(true); }
                using var key = _root.CreateSubKey(_path);
                key.SetValue(_name, _target, RegistryValueKind.DWord);
                if (Read() != _target) throw new InvalidOperationException("Policy read-back failed.");
                return true;
            }
        }
        public bool Revert()
        {
            lock (_gate)
            {
                if (!File.Exists(_backup))
                    throw new InvalidOperationException("This value was not changed by this control; no previous state is available.");
                var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(_backup));
                if (snapshot == null || snapshot.Path != _path || snapshot.Name != _name || snapshot.Target != _target)
                    throw new InvalidOperationException("Policy backup does not match this control.");
                var current = Read();
                if (current != _target && current != snapshot.Before)
                    throw new InvalidOperationException("Policy changed outside Rion; leaving it unchanged.");
                using var key = _root.CreateSubKey(_path);
                if (snapshot.Before.HasValue) key.SetValue(_name, snapshot.Before.Value, RegistryValueKind.DWord);
                else key.DeleteValue(_name, false);
                if (Read() != snapshot.Before) throw new InvalidOperationException("Restore read-back failed.");
                File.Delete(_backup);
                return true;
            }
        }
    }
}
