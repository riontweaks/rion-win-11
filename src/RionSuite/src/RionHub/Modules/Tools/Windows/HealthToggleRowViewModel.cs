using System;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RionHub.Shell;

namespace RionHub.Modules.Tools.Windows;

public sealed class HealthToggleRowViewModel : ObservableObject
{
	private readonly TweakService _svc = new TweakService(new WindowsRegistry());

	private readonly SystemTweak _tweak;

	private bool _isOn;

	private string _error = "";

	public string Title => _tweak.Title;

	public string Summary => _tweak.Summary;

	public bool IsOn
	{
		get
		{
			return _isOn;
		}
		set
		{
			Toggle(value);
		}
	}

	public string Error
	{
		get
		{
			return _error;
		}
		private set
		{
			Set(ref _error, value, "Error");
		}
	}

	public HealthToggleRowViewModel(SystemTweak tweak)
	{
		_tweak = tweak;
		RefreshState();
	}

	public void RefreshState()
	{
		long? current;
		TweakState tweakState = _svc.Inspect(_tweak, out current);
		Set(ref _isOn, tweakState == TweakState.AtTarget, "IsOn");
	}

	private void Toggle(bool value)
	{
		try
		{
			BackupSession backupSession = new BackupSession
			{
				Description = "Windows Health: " + Title
			};
			if (value)
			{
				_svc.Apply(new SystemTweak[1] { _tweak }, backupSession, out var _);
			}
			else
			{
				_svc.Revert(_tweak, backupSession);
			}
			backupSession.Save();
			Error = "";
			ToastCenter.Show(Title + ": " + (value ? "applied" : "reverted") + " successfully.");
		}
		catch (Exception ex)
		{
			Error = ex.Message;
			ToastCenter.Show(Title + ": " + ex.Message, isError: true);
		}
		finally
		{
			RefreshState();
		}
	}
}
