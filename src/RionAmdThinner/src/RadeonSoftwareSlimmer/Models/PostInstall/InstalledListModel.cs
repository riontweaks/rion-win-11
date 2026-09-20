using System;
using System.Collections.Generic;
using System.ComponentModel;
using RadeonSoftwareSlimmer.Intefaces;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Models.PostInstall
{
    public class InstalledListModel : INotifyPropertyChanged
    {
        private readonly IRegistry _registry;
        private readonly bool _amdOnly;
        private IEnumerable<InstalledModel> _installedItems;

        private const string UNINSTALL_REGISTRY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string UNINSTALL_REGISTRY_PATH_WOW6432Node = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
        private readonly string[] AMD_CHIPSET_NAMES =
        {
            "Chipset",
            "GPIO",
            "PCI",
            "PSP",
            "Ryzen",
            "SMBus",
            "3D V-Cache",
            "AMD Application Compatibility Database Driver",
            "PPM Provisioning",
            "AMD Interface",
            "I2C"
        };

        /// <param name="amdOnly">true = the original AMD post-install behavior (only Radeon-published
        /// entries, minus chipset noise); false = every installed desktop app, for a general
        /// debloat/uninstall list.</param>
        public InstalledListModel(IRegistry registry, bool amdOnly = true)
        {
            _registry = registry;
            _amdOnly = amdOnly;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        public IEnumerable<InstalledModel> InstalledItems
        {
            get { return _installedItems; }
            set
            {
                _installedItems = value;
                OnPropertyChanged(nameof(InstalledItems));
            }
        }


        public void LoadOrRefresh()
        {
            InstalledItems = new List<InstalledModel>(GetAllUninstallEntries());
        }

        public void ApplyChanges()
        {
            foreach (InstalledModel install in _installedItems)
            {
                if (install.Uninstall)
                    install.RunUninstaller();
            }
        }


        private IEnumerable<InstalledModel> GetAllUninstallEntries()
        {
            if (!_amdOnly)
            {
                foreach (string path in new[] { UNINSTALL_REGISTRY_PATH, UNINSTALL_REGISTRY_PATH_WOW6432Node })
                using (IRegistryKey userRoot = _registry.CurrentUser.OpenSubKey(path, false))
                {
                    if (userRoot == null) continue;
                    foreach (string name in userRoot.GetSubKeyNames())
                    using (IRegistryKey key = userRoot.OpenSubKey(name, false))
                        if (IsEligible(key)) yield return new InstalledModel(key, name);
                }
            }
            using (IRegistryKey uninstallRootKey = _registry.LocalMachine.OpenSubKey(UNINSTALL_REGISTRY_PATH, false))
            {
                if (uninstallRootKey != null)
                {
                    foreach (string uninstallName in uninstallRootKey.GetSubKeyNames())
                    {
                        using (IRegistryKey uninstallKey = uninstallRootKey.OpenSubKey(uninstallName, false))
                        {
                            if (IsEligible(uninstallKey))
                                yield return new InstalledModel(uninstallKey, uninstallName);
                        }
                    }
                }
            }

            if (Environment.Is64BitOperatingSystem)
            {
                using (IRegistryKey uninstallRootKey = _registry.LocalMachine.OpenSubKey(UNINSTALL_REGISTRY_PATH_WOW6432Node, false))
                {
                    if (uninstallRootKey != null)
                    {
                        foreach (string uninstallName in uninstallRootKey.GetSubKeyNames())
                        {
                            using (IRegistryKey uninstallKey = uninstallRootKey.OpenSubKey(uninstallName, false))
                            {
                                if (IsEligible(uninstallKey))
                                    yield return new InstalledModel(uninstallKey, uninstallName);
                            }
                        }
                    }
                }
            }
        }

        private bool IsEligible(IRegistryKey uninstallKey)
        {
            object displayName = uninstallKey?.GetValue("DisplayName");
            if (displayName == null || string.IsNullOrWhiteSpace(displayName.ToString()))
                return false; // no name to show — Windows itself hides these in Programs & Features

            if (!_amdOnly)
            {
                // Hide sub-entries with no uninstall string at all (nothing we could do) and
                // Windows' own "system component" marked entries, matching what Programs &
                // Features itself would show.
                object systemComponent = uninstallKey.GetValue("SystemComponent");
                if (systemComponent != null && Convert.ToInt32(systemComponent) == 1) return false;
                return uninstallKey.GetValue("UninstallString") != null;
            }

            object publisher = uninstallKey.GetValue("Publisher");
            if (publisher != null
                && publisher.ToString().Equals("Advanced Micro Devices, Inc.", StringComparison.OrdinalIgnoreCase)
                && !Array.Exists(AMD_CHIPSET_NAMES, name => displayName.ToString().Contains(name)))
            {
                StaticViewModel.AddDebugMessage($"Found uninstall item {displayName} under {uninstallKey.Name}");
                return true;
            }

            return false;
        }
    }
}
