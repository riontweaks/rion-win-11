using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
#if NET48
using System.Linq;
#endif
using System.Reflection;
using RadeonSoftwareSlimmer.Services;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Models.PreInstall
{
    public class InstallerFilesModel : INotifyPropertyChanged
    {
        private readonly IFileSystem _fileSystem;
        private string _installerFile;
        private string _extractedInstallerDirectory;
#if NET6_0_OR_GREATER
        // .NET 6+ removed certain characters for reasons I don't like
        // https://github.com/dotnet/runtime/issues/63383
        // https://github.com/dotnet/corefx/pull/8669/files#r63910570
        private readonly char[] extraInvalidDirChars = { '\"', '<', '>', };
#endif


        public InstallerFilesModel(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        public string InstallerFile
        {
            get { return _installerFile; }
            set
            {
                _installerFile = value;
                OnPropertyChanged(nameof(InstallerFile));
            }
        }
        public string ExtractedInstallerDirectory
        {
            get { return _extractedInstallerDirectory; }
            set
            {
                _extractedInstallerDirectory = value;
                OnPropertyChanged(nameof(ExtractedInstallerDirectory));
            }
        }


        public void ExtractInstallerFiles()
        {
            using var installer = InstallerExecutionLease.Acquire(InstallerFile, "AMD");
            using var sevenZip = SevenZip.Acquire();
            ProcessHandler processHandler = new ProcessHandler(sevenZip.ExecutablePath);
            int exitCode = processHandler.RunProcess($"x \"{installer.ExecutablePath}\" -o\"{ExtractedInstallerDirectory}\" -y");

            //https://sevenzip.osdn.jp/chm/cmdline/exit_codes.htm
            if (exitCode != 0)
                throw new IOException($"Extraction failed (7-Zip exit code {exitCode}). See the Activity Log for details.");

            // The small "auto-detect" / minimal web installer extracts fine but has no packages to slim.
            if (!ValidateExtractedLocation())
                throw new IOException(
                    "That file extracted, but it doesn't contain the full Adrenalin package (no Setup.exe / Bin64 / Config). " +
                    "It looks like the small auto-detect web installer - download or pick the full 'AMD Software: Adrenalin Edition' package instead.");
        }

        public void RunRadeonSoftwareSetup()
        {
            StartRadeonSoftwareSetup()?.Dispose();
        }

        /// <summary>Starts Setup.exe and returns the process so callers can wait for it to finish (null if Setup.exe is missing).</summary>
        public Process StartRadeonSoftwareSetup()
        {
            string setup = _fileSystem.Path.Combine(ExtractedInstallerDirectory, "Setup.exe");
            if (!_fileSystem.File.Exists(setup))
            {
                StaticViewModel.AddLogMessage("Setup.exe was not found in " + ExtractedInstallerDirectory + " - extract a full Adrenalin installer first.");
                return null;
            }

            return StartVerifiedAmdExecutable(setup);
        }

        /// <summary>Recursively copies the modified, extracted installer to <paramref name="destination"/>.</summary>
        public void CopyExtractedTo(string destination)
        {
            CopyDirectory(ExtractedInstallerDirectory, destination);
        }

        private void CopyDirectory(string source, string destination)
        {
            _fileSystem.Directory.CreateDirectory(destination);

            foreach (string file in _fileSystem.Directory.GetFiles(source))
            {
                string target = _fileSystem.Path.Combine(destination, _fileSystem.Path.GetFileName(file));
                _fileSystem.File.Copy(file, target, true);
            }

            foreach (string dir in _fileSystem.Directory.GetDirectories(source))
            {
                string name = _fileSystem.Path.GetFileName(dir);
                if (string.Equals(name, "RSS_Backup", StringComparison.OrdinalIgnoreCase))
                    continue;
                CopyDirectory(dir, _fileSystem.Path.Combine(destination, name));
            }
        }

        public void RunAmdCleanupUtility()
        {
            using var process = StartVerifiedAmdExecutable(
                _fileSystem.Path.Combine(ExtractedInstallerDirectory, "Bin64", "AMDCleanupUtility.exe"));
        }

        // Monitoring owns the Process created by Start, including its original kernel
        // handle. Callers receive a separate wrapper which they may dispose independently.
        private static Process StartVerifiedAmdExecutable(string executable)
        {
            var lease = InstallerExecutionLease.Acquire(executable, "AMD");
            Process process = new Process();
            Process caller = null;
            bool started = false;
            try
            {
                process.StartInfo.FileName = lease.ExecutablePath;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.CreateNoWindow = false;
                process.EnableRaisingEvents = true;
                started = process.Start();
                if (!started)
                    throw new IOException("The verified AMD executable did not start.");

                try
                {
                    caller = Process.GetProcessById(process.Id);
                    // Open its own handle before the monitor can dispose the original.
                    _ = caller.SafeHandle;
                    caller.EnableRaisingEvents = true;
                }
                catch (ArgumentException) when (process.HasExited)
                {
                    caller?.Dispose();
                    lease.Dispose();
                    return process;
                }
                _ = ReleaseAfterExitAsync(process, lease, () => process.WaitForExitAsync(), () => process.HasExited);
                return caller;
            }
            catch
            {
                caller?.Dispose();
                if (started)
                {
                    // Failure to create the caller's wrapper does not mean setup exited.
                    _ = ReleaseAfterExitAsync(process, lease, () => process.WaitForExitAsync(), () => process.HasExited);
                }
                else
                {
                    process.Dispose();
                    lease.Dispose();
                }
                throw;
            }
        }

        // Root active resources explicitly: a failed monitor must not leave SafeHandles
        // eligible for finalization while the installer may still be running.
        private static readonly HashSet<(IDisposable Process, IDisposable Lease)> activeInstallerLifetimes = new();

        internal static async System.Threading.Tasks.Task ReleaseAfterExitAsync(
            IDisposable process, IDisposable lease, Func<System.Threading.Tasks.Task> waitForExit, Func<bool> hasExited)
        {
            var lifetime = (process, lease);
            lock (activeInstallerLifetimes) activeInstallerLifetimes.Add(lifetime);
            bool exitConfirmed = false;
            try
            {
                try
                {
                    await waitForExit().ConfigureAwait(false);
                    exitConfirmed = true;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                {
                    try { exitConfirmed = hasExited(); }
                    catch (Exception checkError) when (checkError is InvalidOperationException || checkError is Win32Exception)
                    {
                        StaticViewModel.AddDebugMessage(checkError, "Could not confirm the verified AMD executable exited.");
                    }
                    StaticViewModel.AddDebugMessage(ex, exitConfirmed
                        ? "The verified AMD executable exited after its monitor failed."
                        : "Installer exit is unconfirmed. Its file and path locks remain held until Rion closes.");
                }
            }
            finally
            {
                if (exitConfirmed)
                {
                    lock (activeInstallerLifetimes) activeInstallerLifetimes.Remove(lifetime);
                    try { process.Dispose(); }
                    finally { lease.Dispose(); }
                }
            }
        }

        public bool ValidateInstallerFile()
        {
            if (string.IsNullOrWhiteSpace(_installerFile))
            {
                StaticViewModel.AddLogMessage("Please provide an installer file");
                return false;
            }

            try
            {
                IFileInfo fileInfo = _fileSystem.FileInfo.New(_installerFile);

                if (Array.Exists(_fileSystem.Path.GetInvalidPathChars(), c => fileInfo.DirectoryName.Contains(c)))
                {
                    StaticViewModel.AddLogMessage("File directory contains invalid characters");
                    return false;
                }
                if (Array.Exists(_fileSystem.Path.GetInvalidFileNameChars(), (c => fileInfo.Name.Contains(c))))
                {
                    StaticViewModel.AddLogMessage("File name contains invalid characters");
                    return false;
                }
                if (!fileInfo.Exists)
                {
                    StaticViewModel.AddLogMessage($"Installer file {_installerFile} does not exist or cannot be accessed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // FileInfo.New validates the directory path
                StaticViewModel.AddLogMessage(ex);
                return false;
            }

            return true;
        }

        public bool ValidatePreExtractLocation()
        {
            if (string.IsNullOrWhiteSpace(_extractedInstallerDirectory))
            {
                StaticViewModel.AddLogMessage($"Please enter an extraction path");
                return false;
            }
            try
            {
                IDirectoryInfo directoryInfo = _fileSystem.DirectoryInfo.New(_extractedInstallerDirectory);

                if (Array.Exists(_fileSystem.Path.GetInvalidPathChars(), c => directoryInfo.FullName.Contains(c)))
                {
                    StaticViewModel.AddLogMessage("Directory contains invalid characters");
                    return false;
                }
#if NET6_0_OR_GREATER

                if (Array.Exists(extraInvalidDirChars, c => directoryInfo.FullName.Contains(c)))
                {
                    StaticViewModel.AddLogMessage("Directory contains invalid characters");
                    return false;
                }
#endif

                if (directoryInfo.Exists && (directoryInfo.GetDirectories().Length > 0 || directoryInfo.GetFiles().Length > 0))
                {
                    StaticViewModel.AddLogMessage($"Extraction folder {_extractedInstallerDirectory} is not empty");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // DirectoryInfo.New validates the path
                StaticViewModel.AddLogMessage(ex);
                return false;
            }

            return true;
        }

        public bool ValidateExtractedLocation()
        {
            if (string.IsNullOrWhiteSpace(_extractedInstallerDirectory))
            {
                StaticViewModel.AddLogMessage($"Please enter an extraction path");
                return false;
            }

            try
            {
                IDirectoryInfo directoryInfo = _fileSystem.DirectoryInfo.New(_extractedInstallerDirectory);

                if (Array.Exists(_fileSystem.Path.GetInvalidPathChars(), c => directoryInfo.FullName.Contains(c)))
                {
                    StaticViewModel.AddLogMessage("Directory contains invalid characters");
                    return false;
                }
#if NET6_0_OR_GREATER

                if (Array.Exists(extraInvalidDirChars, c => directoryInfo.FullName.Contains(c)))
                {
                    StaticViewModel.AddLogMessage("Directory contains invalid characters");
                    return false;
                }
#endif

                if (directoryInfo.Exists &&
                    _fileSystem.Directory.Exists(_fileSystem.Path.Combine(_extractedInstallerDirectory, "Bin64")) &&
                    _fileSystem.Directory.Exists(_fileSystem.Path.Combine(_extractedInstallerDirectory, "Config")) &&
                    _fileSystem.File.Exists(_fileSystem.Path.Combine(_extractedInstallerDirectory, "Setup.exe")) &&
                    _fileSystem.File.Exists(_fileSystem.Path.Combine(_extractedInstallerDirectory, "Bin64", "AMDCleanupUtility.exe")))
                {
                    return true;
                }
                else
                {
                    StaticViewModel.AddLogMessage($"Expected installer files not found in {_extractedInstallerDirectory}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                // DirectoryInfo.New validates the path
                StaticViewModel.AddLogMessage(ex);
                return false;
            }
        }
    }
}
