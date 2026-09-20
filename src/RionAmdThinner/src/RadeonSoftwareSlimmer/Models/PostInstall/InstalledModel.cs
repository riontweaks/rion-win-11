using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using RadeonSoftwareSlimmer.Intefaces;
using RadeonSoftwareSlimmer.Services;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Models.PostInstall
{
    public class InstalledModel : INotifyPropertyChanged
    {
        private bool _uninstall;
        private readonly bool _windowsInstaller;
        private string _uninstallExe;
        private string _uninstallArguments;

        public InstalledModel(IRegistryKey uninstallKey, string keyShortName)
        {
            Uninstall = false;

            //Again... no consistency or all the information filled out with this from AMD...
            DisplayName = GetRegistryValueString(uninstallKey, "DisplayName");
            Publisher = GetRegistryValueString(uninstallKey, "Publisher");
            DisplayVersion = GetRegistryValueString(uninstallKey, "DisplayVersion");
            UninstallCommand = GetRegistryValueString(uninstallKey, "UninstallString");
            DisplayIcon = GetRegistryValueString(uninstallKey, "DisplayIcon");
            ProductCode = keyShortName;

            object sizeKb = uninstallKey?.GetValue("EstimatedSize");
            if (sizeKb != null)
            {
                try { EstimatedSizeBytes = Convert.ToInt64(sizeKb, CultureInfo.InvariantCulture) * 1024L; }
                catch { EstimatedSizeBytes = null; }
            }

            string installDate = GetRegistryValueString(uninstallKey, "InstallDate");
            if (!string.IsNullOrWhiteSpace(installDate)
                && DateTime.TryParseExact(installDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime parsed))
            {
                InstallDate = parsed;
            }

            string windowsInstaller = GetRegistryValueString(uninstallKey, "WindowsInstaller");
            if (!string.IsNullOrWhiteSpace(windowsInstaller))
            {
                _windowsInstaller = Convert.ToBoolean(int.Parse(windowsInstaller, CultureInfo.CurrentCulture));
            }

            DetermineUninstallCommand();
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        public bool Uninstall
        {
            get { return _uninstall; }
            set
            {
                _uninstall = value;
                OnPropertyChanged(nameof(Uninstall));
            }
        }
        public string DisplayName { get; }
        public string Publisher { get; }
        public string ProductCode { get; }
        public string DisplayVersion { get; }
        public string UninstallCommand { get; private set; }
        public string DisplayIcon { get; }
        public long? EstimatedSizeBytes { get; }
        public DateTime? InstallDate { get; }
        public string SizeLabel => EstimatedSizeBytes.HasValue ? $"{EstimatedSizeBytes.Value / 1024.0 / 1024.0:0.#} MB" : "—";
        public string InstallDateLabel => InstallDate?.ToString("yyyy-MM-dd") ?? "Unknown";


        /// <summary>MSI-based apps uninstall silently (matches the AMD post-install flow). Everything
        /// else launches the vendor's own uninstaller UI — the same thing Windows' own Apps & Features
        /// "Uninstall" button does for a non-MSI app, since silent flags aren't standardized.</summary>
        public void RunUninstaller()
        {
            if (string.IsNullOrWhiteSpace(UninstallCommand))
            {
                StaticViewModel.AddDebugMessage($"No uninstaller command for {DisplayName}");
                return;
            }

            if (_windowsInstaller)
            {
                ProcessHandler processHandler = new ProcessHandler(_uninstallExe);
                processHandler.RunProcess($"{_uninstallArguments} /quiet /norestart REBOOT=ReallySuppress");
                return;
            }

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = _uninstallExe ?? UninstallCommand;
                    process.StartInfo.Arguments = _uninstallArguments ?? string.Empty;
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                }
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, $"Could not launch uninstaller for {DisplayName}");
            }
        }


        private static string GetRegistryValueString(IRegistryKey registryKey, string valueName)
        {
            if (registryKey == null || string.IsNullOrWhiteSpace(valueName))
                return null;

            object value = registryKey.GetValue(valueName);
            if (value == null)
                return null;

            return value.ToString();
        }

        private void DetermineUninstallCommand()
        {
            if (_windowsInstaller)
            {
                _uninstallExe = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\msiexec.exe";
                if (Guid.TryParse(ProductCode, out Guid productGuid))
                {
                    _uninstallArguments = $"/uninstall {productGuid:B}";
                    UninstallCommand = $"{_uninstallExe} {_uninstallArguments}";
                    StaticViewModel.AddDebugMessage($"Detected GUID {productGuid} from {ProductCode} for {DisplayName}");
                }
                else
                {
                    StaticViewModel.AddDebugMessage($"Unable to determine windows installer GUID from {ProductCode} for {DisplayName}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(UninstallCommand))
            {
                SplitCommandLine(UninstallCommand, out _uninstallExe, out _uninstallArguments);
                StaticViewModel.AddDebugMessage($"Keeping default uninstall command {UninstallCommand} for {DisplayName}");
            }
            else
            {
                StaticViewModel.AddDebugMessage($"Unable to determine uninstall command for {DisplayName}");
            }
        }

        /// <summary>Splits a registry UninstallString into an executable path + arguments. Handles
        /// the common quoted-path form (<c>"C:\...\uninst.exe" /S</c>) and falls back to treating
        /// the whole string as the executable when it isn't quoted.</summary>
        private static void SplitCommandLine(string commandLine, out string exe, out string arguments)
        {
            string trimmed = commandLine.Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = trimmed.IndexOf('"', 1);
                if (end > 0)
                {
                    exe = trimmed.Substring(1, end - 1);
                    arguments = trimmed.Substring(end + 1).Trim();
                    return;
                }
            }

            int space = trimmed.IndexOf(' ');
            if (space < 0)
            {
                exe = trimmed;
                arguments = string.Empty;
            }
            else
            {
                exe = trimmed.Substring(0, space);
                arguments = trimmed.Substring(space + 1).Trim();
            }
        }
    }
}
