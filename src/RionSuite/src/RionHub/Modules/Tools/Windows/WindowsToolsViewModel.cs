// Recovered from the original September 15 executable using ILSpy; original source unavailable.
#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RionHub.Shell;

namespace RionHub.Modules.Tools.Windows;

public sealed class WindowsToolsViewModel : ObservableObject
{
	private bool _isStoreFixBusy;

	private string _storeFixResult = "";

	private InstallerSection _section;

	private bool _isInstalling;

	private bool showRuntimeInstallers;
    private readonly Func<WingetAppEntry, bool, Task<bool>> installWinget;
    private readonly Func<WindowsAppEntry, Task<bool>> installWindows;
    private string installStatus = "";
    public string InstallStatus { get => installStatus; private set => Set(ref installStatus, value); }
    public ObservableCollection<string> InstallResults { get; } = new();
    public bool HasInstallResults => InstallResults.Count > 0;
    public bool CanEditSelection => !IsInstalling;
    public RelayCommand SelectAllRuntimesCommand { get; }
    public RelayCommand ClearRuntimesCommand { get; }
    public RelayCommand SelectCurrentRuntimesCommand { get; }
    private void RefreshCommands()
    {
        InstallSelectedCommand?.RaiseCanExecuteChanged();
        SelectAllRuntimesCommand?.RaiseCanExecuteChanged();
        ClearRuntimesCommand?.RaiseCanExecuteChanged();
        SelectCurrentRuntimesCommand?.RaiseCanExecuteChanged();
        StoreFixCommand?.RaiseCanExecuteChanged();
    }

	public ObservableCollection<HealthToggleRowViewModel> HealthRows { get; }

	public RelayCommand StoreFixCommand { get; }

	public bool IsStoreFixBusy
	{
		get
		{
			return _isStoreFixBusy;
		}
		private set
		{
			if (Set(ref _isStoreFixBusy, value, "IsStoreFixBusy"))
			{
				RefreshCommands();
			}
		}
	}

	public string StoreFixResult
	{
		get
		{
			return _storeFixResult;
		}
		private set
		{
			Set(ref _storeFixResult, value, "StoreFixResult");
		}
	}

	public ObservableCollection<WingetAppRowViewModel> Apps { get; }

	public ObservableCollection<WindowsAppRowViewModel> WindowsApps { get; }

	public ObservableCollection<WingetAppRowViewModel> Runtimes { get; }

	public ObservableCollection<UtilityToolRowViewModel> Utilities { get; }

	public InstallerSection Section
	{
		get
		{
			return _section;
		}
		set
		{
			if (Set(ref _section, value, "Section"))
			{
				Raise("IsAppsView");
				Raise("IsWindowsAppsView");
				Raise("IsRuntimesView");
				Raise("IsUtilitiesView");
				Raise("SelectedCount");
                RefreshCommands();
			}
		}
	}

	public bool IsAppsView => Section == InstallerSection.Apps;

	public bool IsWindowsAppsView => Section == InstallerSection.WindowsApps;

	public bool IsRuntimesView => Section == InstallerSection.Runtimes;

	public bool IsUtilitiesView => Section == InstallerSection.Utilities;

	public int SelectedCount => Section switch
	{
		InstallerSection.Apps => Apps.Count((WingetAppRowViewModel a) => a.IsSelected), 
		InstallerSection.WindowsApps => WindowsApps.Count((WindowsAppRowViewModel a) => a.IsSelected), 
		InstallerSection.Runtimes => Runtimes.Count((WingetAppRowViewModel a) => a.IsSelected), 
		_ => 0, 
	};

	public bool IsInstalling
	{
		get
		{
			return _isInstalling;
		}
		private set
		{
			if (Set(ref _isInstalling, value, "IsInstalling"))
			{
				Raise(nameof(CanEditSelection));
                RefreshCommands();
			}
		}
	}

	public RelayCommand InstallSelectedCommand { get; }

	public bool ShowRuntimeInstallers
	{
		get
		{
			return showRuntimeInstallers;
		}
		set
		{
			Set(ref showRuntimeInstallers, value, "ShowRuntimeInstallers");
		}
	}

	private void SelectRuntimes(Func<WingetAppRowViewModel, bool> selected)
	{
		if (IsInstalling)
		{
			return;
		}
		foreach (WingetAppRowViewModel runtime in Runtimes)
		{
			runtime.IsSelected = selected(runtime);
		}
	}

	public WindowsToolsViewModel() : this(true) { }

    public WindowsToolsViewModel(bool initialize, Func<WingetAppEntry, bool, Task<bool>>? wingetInstaller = null,
        Func<WindowsAppEntry, Task<bool>>? windowsInstaller = null)
    {
        installWinget = wingetInstaller ?? ((entry, interactive) => WingetInstallService.InstallAsync(entry.WingetId, null, default, interactive));
        installWindows = windowsInstaller ?? (async entry => entry.Mechanism switch
        {
            WindowsAppMechanism.Winget => await WingetInstallService.InstallAsync(entry.Identifier, null),
            WindowsAppMechanism.AppxRepair => await AppxRepairService.ReinstallAsync(entry.Identifier, entry.StoreProductId, entry.Name, null),
            WindowsAppMechanism.StoreFix => await RunStoreFixCoreAsync(),
            _ => false
        });
        SelectAllRuntimesCommand = new RelayCommand(() => SelectRuntimes(_ => true), () => !IsInstalling);
        ClearRuntimesCommand = new RelayCommand(() => SelectRuntimes(_ => false), () => !IsInstalling);
        SelectCurrentRuntimesCommand = new RelayCommand(() => SelectRuntimes(r => r.Entry.WingetId.Contains("2015+") || r.Entry.WingetId == "Microsoft.DirectX"), () => !IsInstalling);
		HealthRows = new();
        if (initialize) HealthRows.Add(new HealthToggleRowViewModel(WindowsHealthCatalog.WindowsUpdateOff));
		StoreFixCommand = new RelayCommand(async delegate
		{
			await RunStoreFixAsync();
		}, () => !IsStoreFixBusy && !IsInstalling);
		Apps = new ObservableCollection<WingetAppRowViewModel>(WingetAppCatalog.Apps.Select((WingetAppEntry e) => new WingetAppRowViewModel(e)));
		WindowsApps = new ObservableCollection<WindowsAppRowViewModel>(WindowsAppCatalog.All.Select((WindowsAppEntry e) => new WindowsAppRowViewModel(e)));
		Runtimes = new ObservableCollection<WingetAppRowViewModel>(WingetAppCatalog.Runtimes.Select((WingetAppEntry e) => new WingetAppRowViewModel(e)));
		if (initialize) PortableToolDownloader.MigrateLegacyDownloads();
		Utilities = new ObservableCollection<UtilityToolRowViewModel>(UtilityToolCatalog.All.Select((UtilityToolEntry e) => new UtilityToolRowViewModel(e)));
		foreach (WingetAppRowViewModel app in Apps)
		{
			app.PropertyChanged += OnRowPropertyChanged;
		}
		foreach (WindowsAppRowViewModel windowsApp in WindowsApps)
		{
			windowsApp.PropertyChanged += OnRowPropertyChanged;
		}
		foreach (WingetAppRowViewModel runtime in Runtimes)
		{
			runtime.PropertyChanged += OnRowPropertyChanged;
		}
		InstallSelectedCommand = new RelayCommand(async delegate
		{
			await InstallSelectedAsync();
		}, () => !IsInstalling && !IsStoreFixBusy && SelectedCount > 0);
		if (initialize) _ = RefreshIconsAsync();
	}

	public async Task RefreshIconsAsync()
	{
		_ = 1;
		try
		{
			Dictionary<string, string> dictionary = await Task.Run(() => InstallerIcons.ReadDesktopIcons(Apps.Select((WingetAppRowViewModel row) => row.Name)));
			foreach (WingetAppRowViewModel app in Apps)
			{
				app.IconPath = dictionary.GetValueOrDefault(app.Name, "");
			}
			WindowsApps.First((WindowsAppRowViewModel row) => row.Name == "OneDrive").IconPath = ""; // Use the bundled cloud vector; installed shell icons can be blank.
			Dictionary<string, string> dictionary2 = await InstallerIcons.ReadStoreIconsAsync();
			foreach (WindowsAppRowViewModel item in WindowsApps.Where((WindowsAppRowViewModel row) => row.Entry.Mechanism != WindowsAppMechanism.Winget))
			{
				item.IconPath = dictionary2.GetValueOrDefault(item.Entry.Identifier ?? "Microsoft.WindowsStore_8wekyb3d8bbwe", "");
			}
		}
		catch (Exception ex)
		{
			Trace.TraceWarning("Installer icon refresh failed: {0}", ex.Message);
		}
	}

	private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		Raise("SelectedCount");
        RefreshCommands();
	}

	private async Task RunStoreFixAsync()
	{
        if (IsStoreFixBusy || IsInstalling) return;
		IsStoreFixBusy = true;
		StoreFixResult = "";
		IProgressToastHandle toast = ToastCenter.ShowProgress("Microsoft Store Fix…");
		try
		{
			FixResult fixResult = await RunStoreFixRawAsync();
			StoreFixResult = fixResult.Message;
			if (fixResult.Success)
			{
				toast.Complete(fixResult.Message);
			}
			else
			{
				toast.Fail(fixResult.Message);
			}
		}
		catch (Exception ex)
		{
			StoreFixResult = "Fix failed: " + ex.Message;
			toast.Fail(StoreFixResult);
		}
		finally
		{
			IsStoreFixBusy = false;
		}
	}

	private static Task<FixResult> RunStoreFixRawAsync()
	{
		return FixCatalog.All.First((FixDefinition f) => f.Id == "store-fix").Run(new WindowsRegistry(), null, null, default(CancellationToken));
	}

	private static async Task<bool> RunStoreFixCoreAsync()
	{
		try
		{
			return (await RunStoreFixRawAsync()).Success;
		}
		catch
		{
			return false;
		}
	}

    public async Task InstallSelectedAsync()
    {
        if (IsInstalling || IsStoreFixBusy || SelectedCount == 0) return;
        bool interactive = Section == InstallerSection.Runtimes && ShowRuntimeInstallers;
        // Capture the queue before awaiting; switching tabs cannot change an active batch.
        var queue = Section == InstallerSection.WindowsApps
            ? WindowsApps.Where(r => r.IsSelected).Select(r => (r.Name, Run: (Func<Task<bool>>)(() => installWindows(r.Entry)))).ToList()
            : (Section == InstallerSection.Runtimes ? Runtimes : Apps).Where(r => r.IsSelected)
                .Select(r => (r.Name, Run: (Func<Task<bool>>)(() => installWinget(r.Entry, interactive)))).ToList();
        if (queue.Count == 0) return;
        IsInstalling = true;
        InstallResults.Clear(); Raise(nameof(HasInstallResults));
        InstallStatus = $"Installing 0/{queue.Count}…";
        var toast = ToastCenter.ShowProgress(InstallStatus);
        int succeeded = 0;
        try
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var item = queue[i];
                InstallStatus = $"Installing {i + 1}/{queue.Count}: {item.Name}";
                toast.Report(i, queue.Count, InstallStatus);
                try
                {
                    bool success = await item.Run();
                    if (success) succeeded++;
                    InstallResults.Add(item.Name + (success ? ": completed." : ": failed. Check the installer output and retry."));
                }
                catch (Exception ex) { InstallResults.Add(item.Name + ": failed. " + ex.Message); }
                Raise(nameof(HasInstallResults));
            }
            InstallStatus = $"{succeeded} completed; {queue.Count - succeeded} failed.";
            if (succeeded == queue.Count) toast.Complete(InstallStatus); else toast.Fail(InstallStatus);
        }
        finally { IsInstalling = false; }
    }
}
