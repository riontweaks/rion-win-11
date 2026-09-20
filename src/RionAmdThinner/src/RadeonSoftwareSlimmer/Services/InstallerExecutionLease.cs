using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using RadeonSoftwareSlimmer.Models;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>Locks the executable and its path before checking the vendor signature.
    /// Keep alive until the child process exits. This does not validate installer sidecars.</summary>
    public sealed class InstallerExecutionLease : IDisposable
    {
        private readonly List<IDisposable> guards = new();
        private readonly string executablePath;
        private bool disposed;
        public string ExecutablePath => !disposed ? executablePath : throw new ObjectDisposedException(nameof(InstallerExecutionLease));
        private InstallerExecutionLease(string path) => executablePath = path;

        public static InstallerExecutionLease Acquire(string executablePath, string vendor) =>
            Acquire(executablePath, vendor, path => new InstallerValidator().Inspect(path));

        internal static InstallerExecutionLease Acquire(string executablePath, string vendor, Func<string, DriverInstallerInfo> inspect)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
                throw new ArgumentException("An absolute installer path is required.", nameof(executablePath));
            if (vendor != "AMD" && vendor != "NVIDIA")
                throw new ArgumentException("A supported installer publisher is required.", nameof(vendor));
            ArgumentNullException.ThrowIfNull(inspect);
            var lease = new InstallerExecutionLease(Path.GetFullPath(executablePath));
            try
            {
                // Hold each ancestor before resolving the next child. A file handle alone
                // does not stop an attacker from exchanging its containing directory.
                var ancestors = new Stack<string>();
                for (string current = Path.GetDirectoryName(lease.executablePath); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                    ancestors.Push(current);
                while (ancestors.Count > 0) lease.Hold(ancestors.Pop(), directory: true);
                lease.Hold(lease.executablePath, directory: false);
                if (!HistoricalDriverInstaller.Trusted(vendor, inspect(lease.executablePath)))
                    throw new InvalidDataException("Installer signature or publisher verification failed. Setup was not launched.");
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }

        private void Hold(string path, bool directory)
        {
            const uint openReparsePoint = 0x00200000;
            const uint backupSemantics = 0x02000000;
            var handle = CreateFileW(path, directory ? 0x80u : 0x80000000u, FileShare.Read,
                IntPtr.Zero, FileMode.Open, openReparsePoint | (directory ? backupSemantics : 0), IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new IOException("Cannot lock the installer path: " + path, new Win32Exception(error));
            }
            guards.Add(handle);
            if (!GetFileInformationByHandleEx(handle, 9, out AttributeTag info, 8))
                throw new IOException("Cannot inspect the installer path: " + path, new Win32Exception(Marshal.GetLastWin32Error()));
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                ((info.Attributes & FileAttributes.Directory) != 0) != directory)
                throw new IOException("Installer execution requires regular files and directories: " + path);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int i = guards.Count - 1; i >= 0; i--) guards[i].Dispose();
            guards.Clear();
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
    }
}
