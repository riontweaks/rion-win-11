using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>Materializes the embedded upstream 7-Zip pair and verifies both files on every request.</summary>
    public static class SevenZip
    {
        private static readonly object Sync = new object();
        private static readonly byte[] Exe = ReadResource("SevenZip.7z.exe");
        private static readonly byte[] Dll = ReadResource("SevenZip.7z.dll");
        private static readonly string ExeHash = Convert.ToHexString(SHA256.HashData(Exe));
        private static readonly string DllHash = Convert.ToHexString(SHA256.HashData(Dll));

        public static ExecutionLease Acquire() => Acquire(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RionAmdThinner", "7z", ExeHash + "-" + DllHash));

        /// <summary>Keep this lease alive until the extraction process exits.</summary>
        public sealed class ExecutionLease : IDisposable
        {
            private readonly List<IDisposable> guards = new List<IDisposable>();
            private bool disposed;
            private readonly string executablePath;
            public string ExecutablePath => !disposed ? executablePath : throw new ObjectDisposedException(nameof(ExecutionLease));

            internal ExecutionLease(string path) { executablePath = path; }
            internal void Hold(IDisposable guard) => guards.Add(guard);
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                for (int i = guards.Count - 1; i >= 0; i--) guards[i].Dispose();
                guards.Clear();
            }
        }

        internal static ExecutionLease Acquire(string directory)
        {
            directory = Path.GetFullPath(directory);
            var lease = new ExecutionLease(Path.Combine(directory, "7z.exe"));
            try
            {
                // Lock each existing ancestor before resolving its child. Holding only
                // the files would still allow their parent directory to be exchanged.
                var ancestors = new Stack<string>();
                for (string current = directory; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                    ancestors.Push(current);
                while (ancestors.Count > 0)
                {
                    string current = ancestors.Pop();
                    if (!Directory.Exists(current)) Directory.CreateDirectory(current);
                    var handle = CreateFileW(current, 0x80 /* FILE_READ_ATTRIBUTES */, FileShare.Read,
                        IntPtr.Zero, FileMode.Open, 0x02200000 /* BACKUP_SEMANTICS | OPEN_REPARSE_POINT */, IntPtr.Zero);
                    if (handle.IsInvalid)
                    {
                        int error = Marshal.GetLastWin32Error();
                        handle.Dispose();
                        throw new IOException("Cannot lock the 7-Zip cache directory: " + current, new Win32Exception(error));
                    }
                    lease.Hold(handle);
                    if (!GetFileInformationByHandleEx(handle, 9 /* FileAttributeTagInfo */, out AttributeTag info, 8))
                        throw new IOException("Cannot inspect the 7-Zip cache directory: " + current, new Win32Exception(Marshal.GetLastWin32Error()));
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || (info.Attributes & FileAttributes.Directory) == 0)
                        throw new IOException("The 7-Zip cache requires real directories: " + current);
                }
                Prepare(directory);
                HoldVerifiedFile(lease, lease.ExecutablePath, ExeHash);
                HoldVerifiedFile(lease, Path.Combine(directory, "7z.dll"), DllHash);
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }

        private static void HoldVerifiedFile(ExecutionLease lease, string path, string expected)
        {
            var handle = CreateFileW(path, 0x80000000 /* GENERIC_READ */, FileShare.Read,
                IntPtr.Zero, FileMode.Open, 0x00200000 /* OPEN_REPARSE_POINT */, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new IOException("Cannot lock the 7-Zip component: " + path, new Win32Exception(error));
            }
            lease.Hold(handle);
            if (!GetFileInformationByHandleEx(handle, 9, out AttributeTag info, 8))
                throw new IOException("Cannot inspect the 7-Zip component: " + path, new Win32Exception(Marshal.GetLastWin32Error()));
            if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new IOException("The 7-Zip component must be a regular file: " + path);
            var input = new FileStream(handle, FileAccess.Read);
            lease.Hold(input);
            // Recheck the exact handles that remain locked, closing the gap after Prepare.
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(input)), expected, StringComparison.Ordinal))
                throw new InvalidDataException("The cached 7-Zip component differs from the embedded release: " + path);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AttributeTag { public FileAttributes Attributes; public uint ReparseTag; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, FileShare share,
            IntPtr security, FileMode creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass,
            out AttributeTag information, uint size);

        internal static string Prepare(string directory)
        {
            lock (Sync)
            {
                RejectLinks(directory);
                Directory.CreateDirectory(directory);
                RejectLinks(directory);
                string exe = Path.Combine(directory, "7z.exe");
                Materialize(exe, Exe, ExeHash);
                Materialize(Path.Combine(directory, "7z.dll"), Dll, DllHash);
                return exe;
            }
        }

        private static void Materialize(string path, byte[] bytes, string expected)
        {
            RejectLinks(path);
            if (!File.Exists(path))
            {
                string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        stream.Write(bytes, 0, bytes.Length);
                    try { File.Move(staging, path); }
                    catch (IOException) when (File.Exists(path)) { /* Concurrent creation is accepted only after verification below. */ }
                }
                finally { if (File.Exists(staging)) File.Delete(staging); }
            }
            RejectLinks(path);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(input)), expected, StringComparison.Ordinal))
                throw new InvalidDataException("The cached 7-Zip component differs from the embedded release: " + path);
        }

        private static void RejectLinks(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("The 7-Zip cache cannot cross a symbolic link or junction: " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private static byte[] ReadResource(string name)
        {
            using var source = typeof(SevenZip).Assembly.GetManifestResourceStream(name)
                ?? throw new FileNotFoundException("Embedded resource missing: " + name);
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
