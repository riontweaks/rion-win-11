using System;
using System.IO;
using System.Linq;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Finds a winget-installed tool's executable so it can be launched in place, instead of the
    /// app reporting "installed — open from Start" and leaving the user to go find it.
    ///
    /// winget puts things in one of a few predictable places depending on the package's installer
    /// type: portable packages land under its own Packages folder with a shim in Links, while
    /// regular installers land in Program Files. Each is checked cheaply and the first hit wins.
    /// </summary>
    public static class WingetToolLocator
    {
        private static string[] SearchRoots()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new[]
            {
                Path.Combine(local, "Microsoft", "WinGet", "Packages"),
                Path.Combine(local, "Microsoft", "WinGet", "Links"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            };
        }

        /// <summary>
        /// Full path to the tool's executable, or null when it isn't installed.
        /// <paramref name="executableNames"/> comes from the catalog entry's hints.
        /// </summary>
        public static string Find(string wingetId, string[] executableNames)
        {
            if (executableNames == null || executableNames.Length == 0) return null;

            foreach (string root in SearchRoots())
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    // Shallow first: a direct hit in Links or Program Files\<something>\<exe>.
                    foreach (string name in executableNames)
                    {
                        string direct = Path.Combine(root, name);
                        if (File.Exists(direct)) return direct;
                    }

                    // Then package folders — but ONLY ones whose name matches this package. Program
                    // Files has hundreds of subtrees and recursing all of them would stall startup.
                    foreach (string folder in Directory.EnumerateDirectories(root))
                    {
                        if (!Matches(Path.GetFileName(folder), wingetId)) continue;
                        foreach (string name in executableNames)
                        {
                            string candidate = Directory.EnumerateFiles(folder, name, SearchOption.AllDirectories)
                                .FirstOrDefault();
                            if (candidate != null) return candidate;
                        }
                    }
                }
                catch (UnauthorizedAccessException) { /* skip roots we can't read */ }
                catch (IOException) { }
                catch (Exception ex) { StaticViewModel.AddDebugMessage(ex, "Could not search " + root); }
            }

            return null;
        }

        private static bool Matches(string folderName, string wingetId)
        {
            if (string.IsNullOrEmpty(folderName) || string.IsNullOrEmpty(wingetId)) return false;
            // Compare with punctuation and spacing removed, so the winget id
            // "Wagnardsoft.DisplayDriverUninstaller" still matches the installed folder
            // "Display Driver Uninstaller".
            string folder = Simplify(folderName);
            string id = Simplify(wingetId);
            string tail = Simplify(wingetId.Substring(wingetId.LastIndexOf('.') + 1));
            return folder.Contains(id) || folder.Contains(tail) || id.Contains(folder);
        }

        private static string Simplify(string value) =>
            new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
