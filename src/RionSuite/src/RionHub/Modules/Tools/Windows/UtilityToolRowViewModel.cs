using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RionHub.Shell;

namespace RionHub.Modules.Tools.Windows;

public sealed class UtilityToolRowViewModel : ObservableObject
{
	private readonly UtilityToolEntry _entry;
	private readonly Func<string?>? findInstalled;
	private readonly Func<string,Task>? launch;
	private readonly Func<Task<bool>>? install;

	private string? _installedPath;

	private readonly HashSet<string> _rejectedDirectDownloadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private bool _isBusy;

	private string _status = "";

	public string Name => _entry.Name;

	public string Description => _entry.Description;

	public RelayCommand GetCommand { get; }

	public bool IsInstalled => _installedPath != null;

	public string ActionLabel
	{
		get
		{
			if (_entry.Mechanism != UtilityMechanism.DownloadPage)
			{
				if (!IsInstalled)
				{
					return "Get";
				}
				return "Run";
			}
			return "Download page";
		}
	}

	public string LocationHint => _installedPath ?? "";

	public bool IsBusy
	{
		get
		{
			return _isBusy;
		}
		private set
		{
			if (Set(ref _isBusy, value, "IsBusy"))
			{
				GetCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string Status
	{
		get
		{
			return _status;
		}
		private set
		{
			Set(ref _status, value, "Status");
		}
	}

	public UtilityToolRowViewModel(UtilityToolEntry entry, Func<string?>? findInstalled=null, Func<string,Task>? launch=null, Func<Task<bool>>? install=null)
	{
		if (entry.Mechanism != UtilityMechanism.DownloadPage && entry.Sha256 == null && string.IsNullOrWhiteSpace(entry.ExpectedPublisher))
		{
			throw new ArgumentException("Utilities require a publisher identity or SHA-256 fingerprint.", "entry");
		}
		_entry = entry;
		this.findInstalled=findInstalled;this.launch=launch;this.install=install;
		GetCommand = new RelayCommand(async delegate
		{
			await RunAsync();
		}, () => !IsBusy);
		Refresh();
	}

	public void Refresh()
	{
		if(findInstalled!=null){_installedPath=findInstalled();Raise("IsInstalled");Raise("ActionLabel");Raise("LocationHint");return;}
		if (_entry.Mechanism == UtilityMechanism.DownloadPage)
		{
			return;
		}
		if (_entry.Mechanism == UtilityMechanism.DirectDownload)
		{
			_installedPath = SkipRejectedCandidate(PortableToolDownloader.FindDownloaded(_entry.FileName, _entry.MinSizeBytes));
			if (_installedPath == null && _entry.FallbackWingetId != null)
			{
				_installedPath = SkipRejectedCandidate(WingetToolLocator.Find(_entry.FallbackWingetId, _entry.ExecutableHints));
			}
		}
		else
		{
			_installedPath = WingetToolLocator.Find(_entry.Source, _entry.ExecutableHints);
		}
		Raise("IsInstalled");
		Raise("ActionLabel");
		Raise("LocationHint");
	}

	private string? SkipRejectedCandidate(string? path)
	{
		if (path == null || !_rejectedDirectDownloadPaths.Contains(path))
		{
			return path;
		}
		return null;
	}

	private async Task LaunchInstalledAsync()
	{
		IsBusy = true;
		string path = _installedPath;
		try
		{
			if(launch!=null)await launch(path);
			else await Task.Run(() => PortableToolDownloader.LaunchDownloaded(path, _entry.Sha256, _entry.ExpectedPublisher));
			Status = "Launched.";
		}
		catch (Exception ex)
		{
			if (ex is InvalidDataException && _entry.Mechanism == UtilityMechanism.DirectDownload)
			{
				_rejectedDirectDownloadPaths.Add(path);
				Status = "This copy failed verification. Use Get to download the catalog version. " + ex.Message;
			}
			else
			{
				Status = "Error: " + ex.Message;
			}
			ToastCenter.Show(Name + ": " + Status, isError: true);
			Refresh();
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task RunAsync()
	{
		Refresh(); // An external uninstall must not leave a stale Run action.
		if (_entry.Mechanism == UtilityMechanism.DownloadPage)
		{
			try
			{
				Uri uri = new Uri(_entry.Source);
				if (uri.Scheme != Uri.UriSchemeHttps)
				{
					throw new InvalidOperationException("The download page must use HTTPS.");
				}
				Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
				{
					UseShellExecute = true
				});
				Status = "Opened the author's download page.";
				return;
			}
			catch (Exception ex)
			{
				Status = "Could not open download page: " + ex.Message;
				return;
			}
		}
		if (IsInstalled)
		{
			await LaunchInstalledAsync();
			return;
		}
		IsBusy = true;
		Status = "Working…";
		IProgressToastHandle toast = ToastCenter.ShowProgress("Getting " + Name + "…");
		try
		{
			bool flag;
			if(install!=null)flag=await install();
			else if (_entry.Mechanism == UtilityMechanism.DirectDownload)
			{
				Progress<double> progress = new Progress<double>(delegate(double p)
				{
					if (p >= 0.0)
					{
						toast.Report((int)(p * 100.0), 100, $"Downloading {Name}… {(int)(p * 100.0)}%");
					}
				});
				flag = await PortableToolDownloader.DownloadAndLaunchAsync(_entry.Source, _entry.FileName, progress, _entry.MinSizeBytes, default(CancellationToken), _entry.Sha256, _entry.ExpectedPublisher);
			}
			else
			{
				flag = await WingetInstallService.InstallAsync(_entry.Source, null);
			}
			if (flag && _entry.Mechanism == UtilityMechanism.DirectDownload)
			{
				_rejectedDirectDownloadPaths.Clear();
			}
			Refresh();
			if (flag && _entry.Mechanism == UtilityMechanism.Winget)
			{
				if (IsInstalled)
				{
					Status = "Installed — use Run to launch it.";
					toast.Complete(Name + " installed. Use Run to launch it.");
				}
				else
				{
					Status = "Installed / up to date — open from Start.";
					toast.Complete(Name + " installed / up to date — open from Start.");
				}
			}
			else if (flag)
			{
				Status = "Downloaded to " + PortableToolDownloader.ToolsDir + " — launched.";
				toast.Complete(Name + " downloaded and launched.");
			}
			else
			{
				Status = "Could not complete this operation.";
				toast.Fail("Could not get " + Name + ".");
			}
		}
		catch (Exception ex2)
		{
			Status = "Error: " + ex2.Message;
			toast.Fail(Name + ": " + ex2.Message);
			Refresh();
		}
		finally
		{
			IsBusy = false;
		}
	}
}
