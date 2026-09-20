using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Intefaces;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    public static class StartupInventory
    {
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string RunKey32 = "Software\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ApprovedRun = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run";
        private const string ApprovedRun32 = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run32";
        private const string ApprovedFolder = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\StartupFolder";

        private static readonly byte[] EnabledBlob = { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public static IReadOnlyList<StartupEntry> Enumerate(IRegistry registry)
        {
            var entries = new List<StartupEntry>();

            ReadRunKey(registry, false, RunKey, ApprovedRun, StartupLocation.CurrentUserRun, entries);
            ReadRunKey(registry, true, RunKey, ApprovedRun, StartupLocation.LocalMachineRun, entries);
            ReadRunKey(registry, true, RunKey32, ApprovedRun32, StartupLocation.LocalMachineRun32, entries);

            ReadStartupFolder(registry, Environment.SpecialFolder.Startup, StartupLocation.CurrentUserStartupFolder, entries);
            ReadStartupFolder(registry, Environment.SpecialFolder.CommonStartup, StartupLocation.CommonStartupFolder, entries);

            return entries
                .OrderBy(e => e.IsEnabled ? 0 : 1)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ReadRunKey(IRegistry registry, bool localMachine, string runPath, string approvedPath,
            StartupLocation location, List<StartupEntry> into)
        {
            IRegistryKey root = localMachine ? registry.LocalMachine : registry.CurrentUser;
            try
            {
                using (IRegistryKey run = root.OpenSubKey(runPath, false))
                {
                    if (run == null) return;

                    using (IRegistryKey approved = root.OpenSubKey(approvedPath, false))
                    {
                        foreach (string name in run.GetValueNames())
                        {
                            if (string.IsNullOrEmpty(name)) continue;
                            string command = Convert.ToString(run.GetValue(name));
                            bool enabled = IsApprovedEnabled(approved, name);
                            into.Add(new StartupEntry(name, command, location, enabled));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not read " + runPath);
            }
        }

        private static void ReadStartupFolder(IRegistry registry, Environment.SpecialFolder folder,
            StartupLocation location, List<StartupEntry> into)
        {
            try
            {
                string path = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

                using (IRegistryKey approved = registry.CurrentUser.OpenSubKey(ApprovedFolder, false))
                {
                    foreach (string file in Directory.GetFiles(path))
                    {
                        string name = Path.GetFileName(file);
                        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        bool enabled = IsApprovedEnabled(approved, name);
                        into.Add(new StartupEntry(Path.GetFileNameWithoutExtension(file), file, location, enabled));
                    }
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Could not read startup folder " + folder);
            }
        }

        private static bool IsApprovedEnabled(IRegistryKey approved, string name)
        {
            if (approved == null) return true;
            object raw = approved.GetValue(name);
            if (raw is byte[] blob && blob.Length > 0)
                return (blob[0] & 1) == 0;   // even first byte = enabled, odd = disabled
            return true;
        }

        /// <summary>Enable/disable an entry via its StartupApproved blob. Returns the previous enabled state.</summary>
        public static bool SetEnabled(IRegistry registry, StartupEntry entry, bool enable)
        {
            string approvedPath;
            IRegistryKey root;
            switch (entry.Location)
            {
                case StartupLocation.CurrentUserRun:
                    root = registry.CurrentUser; approvedPath = ApprovedRun; break;
                case StartupLocation.LocalMachineRun:
                    root = registry.LocalMachine; approvedPath = ApprovedRun; break;
                case StartupLocation.LocalMachineRun32:
                    root = registry.LocalMachine; approvedPath = ApprovedRun32; break;
                default:
                    root = registry.CurrentUser; approvedPath = ApprovedFolder; break;
            }

            string valueName = entry.IsRegistryEntry ? entry.Name : Path.GetFileName(entry.Command);

            using (IRegistryKey approved = root.OpenSubKey(approvedPath, true) ?? root.CreateSubKey(approvedPath))
            {
                bool wasEnabled = IsApprovedEnabled(approved, valueName);

                byte[] data;
                if (enable)
                {
                    data = (byte[])EnabledBlob.Clone();
                }
                else
                {
                    data = new byte[12];
                    data[0] = 3;
                    long now = DateTime.UtcNow.ToFileTimeUtc();
                    Array.Copy(BitConverter.GetBytes(now), 0, data, 4, 8);
                }

                approved.SetValue(valueName, data, RegistryValueKind.Binary);
                return wasEnabled;
            }
        }
    }
}
