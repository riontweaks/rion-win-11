using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Models.PreInstall;
using RadeonSoftwareSlimmer.Services;

namespace RadeonSoftwareSlimmer.ViewModels
{
    public class PreInstallViewModel : INotifyPropertyChanged
    {
        private readonly IFileSystem _fileSystem;
        private readonly DriverDownloadService _downloader = new DriverDownloadService();
        private bool _isDownloading;
        private double _downloadProgress;

        public PreInstallViewModel(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            FlipViewIndex = WizardIndex.SelectInstaller;
            InstallerAlreadyExtracted = false;

            InstallerFiles = new InstallerFilesModel(_fileSystem);
            PackageList = new PackageListModel(_fileSystem);
            ScheduledTaskList = new ScheduledTaskXmlListModel(_fileSystem);
            DisplayComponentList = new DisplayComponentListModel(_fileSystem);
        }

        public bool IsDownloading
        {
            get { return _isDownloading; }
            private set { _isDownloading = value; OnPropertyChanged(nameof(IsDownloading)); }
        }

        public double DownloadProgress
        {
            get { return _downloadProgress; }
            private set { _downloadProgress = value; OnPropertyChanged(nameof(DownloadProgress)); }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        public InstallerFilesModel InstallerFiles { get; }
        public PackageListModel PackageList { get; }
        public ScheduledTaskXmlListModel ScheduledTaskList { get; }
        public DisplayComponentListModel DisplayComponentList { get; }
        public bool InstallerAlreadyExtracted { get; set; }
        public WizardIndex FlipViewIndex { get; set; }


        public enum WizardIndex : int
        {
            Empty = -1,
            SelectInstaller = 0,
            SelectExtractLocation = 1,
            ExtractingInstaller = 2,
            ModifyInstaller = 3,
            InstallerDone = 4,
        }


        public void SkipInstallFile()
        {
            InstallerAlreadyExtracted = true;
            FlipViewIndex = WizardIndex.SelectExtractLocation;
        }

        public void ValidateInstallerFile()
        {
            if (InstallerFiles.ValidateInstallerFile())
            {
                FlipViewIndex = WizardIndex.SelectExtractLocation;
            }
        }

        public void BrowseForInstallerFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Radeon Software Installers|*radeon*.exe;*adrenalin*.exe;*amd-software-pro-edition*.exe;*vanguard*.exe|Executables (*.exe)|*.exe|All Files (*.*)|*.*";
            openFileDialog.CheckFileExists = true;
            openFileDialog.Multiselect = false;

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                InstallerFiles.InstallerFile = openFileDialog.FileName;
                IFileInfo file = _fileSystem.FileInfo.New(openFileDialog.FileName);
                InstallerFiles.ExtractedInstallerDirectory = _fileSystem.Path.Combine(file.DirectoryName, file.Name.Substring(0, file.Name.Length - file.Extension.Length));
            }
        }


        public void Back()
        {
            FlipViewIndex = WizardIndex.SelectInstaller;
            InstallerAlreadyExtracted = false;
        }

        public void ValidateExtractLocation()
        {
            if (InstallerAlreadyExtracted && InstallerFiles.ValidateExtractedLocation())
            {
                FlipViewIndex = WizardIndex.ModifyInstaller;
            }
            else if (InstallerFiles.ValidatePreExtractLocation())
            {
                FlipViewIndex = WizardIndex.ExtractingInstaller;
            }
        }

        public void BrowseForExtractLocation()
        {
            
            Microsoft.Win32.OpenFolderDialog folderBrowserDialog = new Microsoft.Win32.OpenFolderDialog();
            {

                if (_fileSystem.Directory.Exists(InstallerFiles.ExtractedInstallerDirectory))
                    folderBrowserDialog.InitialDirectory = InstallerFiles.ExtractedInstallerDirectory;

                if (folderBrowserDialog.ShowDialog() == true)
                {
                    InstallerFiles.ExtractedInstallerDirectory = folderBrowserDialog.FolderName;
                }
            }
        }


        public async Task ExtractInstallerFilesAsync()
        {
            try
            {
                StaticViewModel.IsLoading = true;
                StaticViewModel.AddLogMessage("Extracting installer files");

                await Task.Run(() => InstallerFiles.ExtractInstallerFiles());
                FlipViewIndex = WizardIndex.ModifyInstaller;

                StaticViewModel.AddLogMessage("Installer files extraction complete");
            }
            catch (Exception ex)
            {
                StaticViewModel.AddLogMessage(ex, "Extracting installer files failed");
                FlipViewIndex = WizardIndex.SelectExtractLocation;   // don't loop / don't land on an empty Modify page
            }
            finally
            {
                StaticViewModel.IsLoading = false;
            }
        }


        public void SelectNewInstaller()
        {
            InstallerFiles.InstallerFile = string.Empty;
            InstallerFiles.ExtractedInstallerDirectory = string.Empty;
            InstallerAlreadyExtracted = false;
            FlipViewIndex = WizardIndex.SelectInstaller;
        }

        public void ReadFromExtractedInstaller()
        {
            try
            {
                StaticViewModel.IsLoading = true;
                StaticViewModel.AddLogMessage("Loading installer information");

                IDirectoryInfo extractedDirectory = _fileSystem.DirectoryInfo.New(InstallerFiles.ExtractedInstallerDirectory);
                PackageList.LoadOrRefresh(extractedDirectory);
                ScheduledTaskList.LoadOrRefresh(extractedDirectory);
                DisplayComponentList.LoadOrRefresh(extractedDirectory);

                StaticViewModel.AddLogMessage("Finished loading installer information");
            }
            catch (Exception ex)
            {
                StaticViewModel.AddLogMessage(ex, "Reading from extracted installer location failed");
            }
            finally
            {
                StaticViewModel.IsLoading = false;
            }
        }

        public void Packages_SetAll(bool keep)
        {
            if (PackageList.InstallerPackages == null)
                return;

            foreach (PackageModel package in PackageList.InstallerPackages)
            {
                package.Keep = keep;
            }
        }

        public void ScheduledTask_SetAll(bool enabled)
        {
            if (ScheduledTaskList.ScheduledTasks == null)
                return;

            foreach (ScheduledTaskXmlModel scheduledTask in ScheduledTaskList.ScheduledTasks)
            {
                scheduledTask.Enabled = enabled;
            }
        }

        public void DisplayComponents_SetAll(bool keep)
        {
            if (DisplayComponentList.DisplayDriverComponents == null)
                return;

            foreach (DisplayComponentModel displayComponent in DisplayComponentList.DisplayDriverComponents)
            {
                displayComponent.Keep = keep;
            }
        }

        public string LastModificationError { get; private set; } = "";
        public void ModifyInstaller()
        {
            LastModificationError = "";
            if (!InstallerFiles.ValidateExtractedLocation())
            {
                LastModificationError = "Extract a complete Adrenalin installer before preparing it.";
                StaticViewModel.AddLogMessage("Nothing to modify - extract a full Adrenalin installer first.");
                return;
            }

            try
            {
                StaticViewModel.IsLoading = true;
                StaticViewModel.AddLogMessage("Modifying installer");

                if (PackageList.InstallerPackages != null)
                {
                    foreach (PackageModel package in PackageList.InstallerPackages.Where(p => !p.Keep))
                    {
                        PackageListModel.RemovePackage(package);
                    }
                }

                if (ScheduledTaskList.ScheduledTasks != null)
                {
                    foreach (ScheduledTaskXmlModel task in ScheduledTaskList.ScheduledTasks)
                    {
                        ScheduledTaskList.SetScheduledTaskStatusAndUnhide(task);
                    }
                }

                DisplayComponentList.RemoveComponentsNotKeeping();

                ReadFromExtractedInstaller();

                StaticViewModel.AddLogMessage("Finished modifying installer");
            }
            catch (Exception ex)
            {
                LastModificationError = ex.Message;
                StaticViewModel.AddLogMessage(ex, "Modifying installer failed");
            }
            finally
            {
                StaticViewModel.IsLoading = false;
            }
        }

        public void ResetInstallerToDefaults()
        {
            try
            {
                StaticViewModel.IsLoading = true;
                StaticViewModel.AddLogMessage("Resetting installer to defaults");

                PackageList.RestoreToDefault();
                ScheduledTaskList.RestoreToDefault();
                DisplayComponentList.RestoreToDefault();

                ReadFromExtractedInstaller();

                StaticViewModel.AddLogMessage("Finished resetting installer to defaults");
            }
            catch (Exception ex)
            {
                StaticViewModel.AddLogMessage(ex, "Resetting installer to defaults failed");
            }
            finally
            {
                StaticViewModel.IsLoading = false;
            }
        }


        public void RunRadeonSoftwareSetup()
        {
            InstallerFiles.RunRadeonSoftwareSetup();
        }

        /// <summary>Starts the modified Setup.exe and returns the process so the caller can auto-run tweaks afterwards.</summary>
        public Process StartModifiedInstaller()
        {
            return InstallerFiles.StartRadeonSoftwareSetup();
        }

        public void RunAmdCleanupUtility()
        {
            InstallerFiles.RunAmdCleanupUtility();
        }

        /// <summary>Downloads AMD's current auto-detect installer, then jumps to the extract-location step.</summary>
        public async Task DownloadLatestDriverAsync()
        {
            if (IsDownloading)
                return;

            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog();
            {
                dialog.Title = "Where should the AMD installer be downloaded?";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (dialog.ShowDialog() != true)
                    return;

                try
                {
                    IsDownloading = true;
                    DownloadProgress = 0;
                    StaticViewModel.IsLoading = true;

                    string url = await _downloader.ResolveLatestInstallerUrlAsync().ConfigureAwait(true);
                    IProgress<double> progress = new Progress<double>(p => DownloadProgress = p < 0 ? DownloadProgress : p);
                    string file = await _downloader
                        .DownloadAsync(url, dialog.FolderName, progress, DriverDownloadService.DriversPage)
                        .ConfigureAwait(true);

                    InstallerFiles.InstallerFile = file;
                    IFileInfo info = _fileSystem.FileInfo.New(file);
                    InstallerFiles.ExtractedInstallerDirectory = _fileSystem.Path.Combine(
                        info.DirectoryName, info.Name.Substring(0, info.Name.Length - info.Extension.Length));

                    FlipViewIndex = WizardIndex.SelectExtractLocation;
                    OnPropertyChanged(nameof(FlipViewIndex));
                }
                catch (Exception ex)
                {
                    StaticViewModel.AddLogMessage(ex, "Automatic driver download failed - use Browse instead");
                }
                finally
                {
                    IsDownloading = false;
                    StaticViewModel.IsLoading = false;
                }
            }
        }

        /// <summary>
        /// Downloads the full "Adrenalin Edition" package for the detected GPU series (the one the
        /// slimmer can actually strip), then jumps to the extract-location step. Falls back to
        /// opening AMD's product page in the browser if the link can't be resolved.
        /// </summary>
        public async Task DownloadGpuSeriesPackageAsync()
        {
            if (IsDownloading)
                return;

            string gpuName = AmdInventory.GpuName(new Services.WindowsRegistry());
            if (string.IsNullOrWhiteSpace(gpuName))
            {
                StaticViewModel.AddLogMessage("No AMD GPU detected — use 'Download auto-detect installer' or Browse.");
                return;
            }

            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog();
            {
                dialog.Title = "Where should the AMD driver package be downloaded?";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (dialog.ShowDialog() != true)
                    return;

                try
                {
                    IsDownloading = true;
                    DownloadProgress = 0;
                    StaticViewModel.IsLoading = true;

                    DriverDownloadService.SeriesPackage pkg =
                        await _downloader.ResolveGpuSeriesPackageAsync(gpuName).ConfigureAwait(true);

                    IProgress<double> progress = new Progress<double>(p => DownloadProgress = p < 0 ? DownloadProgress : p);
                    // drivers.amd.com direct links are referer-gated — pass the product page.
                    string file = await _downloader
                        .DownloadAsync(pkg.InstallerUrl, dialog.FolderName, progress, pkg.ProductPageUrl)
                        .ConfigureAwait(true);

                    InstallerFiles.InstallerFile = file;
                    IFileInfo info = _fileSystem.FileInfo.New(file);
                    InstallerFiles.ExtractedInstallerDirectory = _fileSystem.Path.Combine(
                        info.DirectoryName, info.Name.Substring(0, info.Name.Length - info.Extension.Length));

                    FlipViewIndex = WizardIndex.SelectExtractLocation;
                    OnPropertyChanged(nameof(FlipViewIndex));
                }
                catch (Exception ex)
                {
                    StaticViewModel.AddLogMessage(ex, "Could not auto-download the GPU package");
                    TryOpenProductPage(gpuName);
                }
                finally
                {
                    IsDownloading = false;
                    StaticViewModel.IsLoading = false;
                }
            }
        }

        private void TryOpenProductPage(string gpuName)
        {
            try
            {
                var pkg = _downloader.ResolveGpuSeriesPackageAsync(gpuName).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(pkg?.ProductPageUrl))
                {
                    StaticViewModel.AddLogMessage("Opening " + pkg.ProductPageUrl + " — download the full package manually, then Browse to it.");
                    Process.Start(new ProcessStartInfo(pkg.ProductPageUrl) { UseShellExecute = true });
                }
            }
            catch
            {
                StaticViewModel.AddLogMessage("Opening amd.com/support so you can pick your GPU's driver manually.");
                try { Process.Start(new ProcessStartInfo("https://www.amd.com/en/support") { UseShellExecute = true }); } catch { }
            }
        }

        /// <summary>Copies the modified, extracted installer to a folder the user picks.</summary>
        public void SaveModifiedInstaller()
        {
            if (!InstallerFiles.ValidateExtractedLocation())
                return;

            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog();
            {
                dialog.Title = "Save the modified installer to...";
                if (dialog.ShowDialog() != true)
                    return;

                try
                {
                    StaticViewModel.IsLoading = true;
                    string target = dialog.FolderName;

                    if (_fileSystem.Directory.Exists(target) &&
                        (_fileSystem.Directory.GetFiles(target).Length > 0 || _fileSystem.Directory.GetDirectories(target).Length > 0))
                    {
                        target = _fileSystem.Path.Combine(target,
                            _fileSystem.Path.GetFileName(InstallerFiles.ExtractedInstallerDirectory.TrimEnd(Path.DirectorySeparatorChar)));
                    }

                    StaticViewModel.AddLogMessage("Saving modified installer to " + target);
                    InstallerFiles.CopyExtractedTo(target);
                    StaticViewModel.AddLogMessage("Saved. Run Setup.exe from that folder to install.");
                }
                catch (Exception ex)
                {
                    StaticViewModel.AddLogMessage(ex, "Saving the modified installer failed");
                }
                finally
                {
                    StaticViewModel.IsLoading = false;
                }
            }
        }
    }
}
