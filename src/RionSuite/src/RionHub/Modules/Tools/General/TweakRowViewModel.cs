using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RionHub.Shell;

namespace RionHub.Modules.Tools.General;

public sealed class TweakRowViewModel : ObservableObject
{
	private static readonly SemaphoreSlim InspectionSlots = new SemaphoreSlim(2);

	private readonly TweakService _svc = new TweakService(new WindowsRegistry());

	private readonly string? categoryOverride;

	private bool _isLocked;

	private bool _isBusy;

	private string _stateLabel = "Not inspected";

	private PresetOptionViewModel? _selectedPreset;

	private bool _isOn;

	private string _currentValueLabel = "";

	private bool _isFlyoutOpen;

	private RelayCommand? _toggleFlyoutCommand;

	private string _error = "";

	private string _lastResult = "";

	public SystemTweak Tweak { get; }

	public string Title => Tweak.Title;

	public string Summary => Tweak.Summary;

	public string TradeOff => Tweak.TradeOff;

	public string Group
	{
		get
		{
			if (!string.IsNullOrEmpty(Tweak.Group) || categoryOverride == null)
			{
				return OptimizerImportCatalog.GroupFor(Tweak);
			}
			return categoryOverride;
		}
	}

	public string Section => TweakSections.For(Tweak, Group);

	public bool HighRisk
	{
		get
		{
			bool flag = Tweak.SecuritySensitive;
			if (!flag)
			{
				bool flag2;
				switch (Tweak.Id)
				{
				case "vbs-hvci-off":
				case "dynamic-tick-off":
				case "tdr-delay":
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			return flag;
		}
	}

	public string GradeLabel => Tweak.GradeLabel;

	public string GradeLetter => Tweak.Grade.ToString();

	public bool RebootRequired => Tweak.RebootRequired;

	public bool IsAdjustable => Tweak.IsAdjustable;

	public bool IsFeatured => Tweak.IsFeatured;

	public bool CanToggle
	{
		get
		{
			if (!IsLocked && !IsBusy)
			{
				if (!_isOn)
				{
					return string.IsNullOrEmpty(Tweak.ApplyBlockedReason);
				}
				return true;
			}
			return false;
		}
	}

	public bool IsLocked
	{
		get
		{
			return _isLocked;
		}
		set
		{
			_isLocked = value;
			Raise("CanToggle");
			Raise("CanApplySelected");
		}
	}

	public bool IsBusy
	{
		get
		{
			return _isBusy;
		}
		private set
		{
			Set(ref _isBusy, value, "IsBusy");
			Raise("CanToggle");
			Raise("CanApplySelected");
		}
	}

	public bool CanInspect
	{
		get
		{
			if (Tweak.DeferInitialInspection)
			{
				return !Tweak.InspectOnLoad;
			}
			return false;
		}
	}

	public bool CanRestoreSaved
	{
		get
		{
			if (!IsAdjustable)
			{
				Func<bool> hasSavedState = Tweak.HasSavedState;
				if (hasSavedState != null && hasSavedState())
				{
					if (Tweak.InspectOnLoad)
					{
						return !_isOn;
					}
					return true;
				}
				return false;
			}
			return true;
		}
	}

	public string StateLabel
	{
		get
		{
			return _stateLabel;
		}
		private set
		{
			Set(ref _stateLabel, value, "StateLabel");
		}
	}

	public RelayCommand InspectCommand => new RelayCommand(async delegate
	{
		await InspectAsync();
	});

	public RelayCommand RestoreSavedCommand => new RelayCommand(async delegate
	{
		await RestoreSavedAsync();
	});

	public string ApplyAvailability => Tweak.ApplyBlockedReason ?? "";

	public bool CanBulkApply
	{
		get
		{
			if (!Tweak.ManualOnly && !IsAdjustable && !HighRisk)
			{
				return string.IsNullOrEmpty(Tweak.ApplyBlockedReason);
			}
			return false;
		}
	}

	public bool Recommended => GeneralTweakCatalog.IsRecommended(Tweak);

	public ObservableCollection<PresetOptionViewModel>? Presets { get; }

	public PresetOptionViewModel? SelectedPreset
	{
		get
		{
			return _selectedPreset;
		}
		set
		{
			Set(ref _selectedPreset, value, "SelectedPreset");
			Raise("CanApplySelected");
		}
	}

	public bool CanApplySelected
	{
		get
		{
			if (SelectedPreset != null && !IsBusy)
			{
				return !IsLocked;
			}
			return false;
		}
	}

	public bool IsOn
	{
		get
		{
			return _isOn;
		}
		set
		{
			ToggleAsync(value);
		}
	}

	public string CurrentValueLabel
	{
		get
		{
			return _currentValueLabel;
		}
		private set
		{
			Set(ref _currentValueLabel, value, "CurrentValueLabel");
		}
	}

	public bool IsFlyoutOpen
	{
		get
		{
			return _isFlyoutOpen;
		}
		set
		{
			Set(ref _isFlyoutOpen, value, "IsFlyoutOpen");
		}
	}

	public RelayCommand ToggleFlyoutCommand => _toggleFlyoutCommand ?? (_toggleFlyoutCommand = new RelayCommand(delegate
	{
		IsFlyoutOpen = !IsFlyoutOpen;
	}));

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

	public string LastResult
	{
		get
		{
			return _lastResult;
		}
		private set
		{
			Set(ref _lastResult, value, "LastResult");
		}
	}

	public TweakRowViewModel(SystemTweak tweak, string? categoryOverride = null)
	{
		this.categoryOverride = categoryOverride;
		Tweak = tweak;
		if (tweak.IsAdjustable)
		{
			Presets = new ObservableCollection<PresetOptionViewModel>(tweak.Presets.Select((TweakPreset p) => new PresetOptionViewModel(p, this)));
		}
		if (tweak.InspectOnLoad)
		{
			InspectAsync();
		}
		else if (!tweak.DeferInitialInspection)
		{
			RefreshState();
		}
	}

	private bool ConfirmRisk()
	{
		if (HighRisk || Tweak.IndividualApplyOnly)
		{
			return MessageBox.Show(Title + "\n\n" + TradeOff + "\n\nApply this change?", "Review risky tweak", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes;
		}
		return true;
	}

	public bool MatchesCategory(string categoryFilter)
	{
		if (!(categoryFilter == "All Tweaks"))
		{
			return (categoryOverride ?? Tweak.Category.ToString()) == categoryFilter;
		}
		return true;
	}

	public bool MatchesSearch(string search)
	{
		if (!string.IsNullOrWhiteSpace(search) && !Title.Contains(search, StringComparison.OrdinalIgnoreCase) && !Summary.Contains(search, StringComparison.OrdinalIgnoreCase) && !Group.Contains(search, StringComparison.OrdinalIgnoreCase) && !Section.Contains(search, StringComparison.OrdinalIgnoreCase))
		{
			string valueName = Tweak.ValueName;
			if (valueName == null || !valueName.Contains(search, StringComparison.OrdinalIgnoreCase))
			{
				return Tweak.SourceCommand?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false;
			}
		}
		return true;
	}

	public void RefreshState()
	{
		if (Tweak.DeferInitialInspection)
		{
			InspectAsync(preserveOperationError: true);
			return;
		}
		long? current;
		TweakState state = _svc.Inspect(Tweak, out current);
		SetInspection(state, current);
	}

	private void SetInspection(TweakState state, long? current)
	{
		Set(ref _isOn, state == TweakState.AtTarget, "IsOn");
		Raise("CanToggle");
		Raise("CanRestoreSaved");
		StateLabel = state switch
		{
			TweakState.AtTarget => "At requested configuration", 
			TweakState.Unreadable => "State unavailable", 
			_ => "Different configuration", 
		};
		if (!IsAdjustable)
		{
			return;
		}
		string currentValueLabel = ((state == TweakState.Unreadable) ? "Unavailable" : "Not set or custom value");
		if (current.HasValue)
		{
			List<TweakPreset> list = Tweak.Presets.Where((TweakPreset p) => p.Value == current.Value).ToList();
			currentValueLabel = ((list.Count > 0) ? list[0].Label : current.Value.ToString());
		}
		CurrentValueLabel = currentValueLabel;
	}

	private async Task InspectAsync(bool preserveOperationError = false)
	{
		if (IsBusy)
		{
			return;
		}
		IsBusy = true;
		StateLabel = "Inspecting…";
		try
		{
			await InspectionSlots.WaitAsync();
			try
			{
				long? current;
				string error;
				(TweakState, long?, string) tuple = await Task.Run(() => (state: _svc.InspectWithError(Tweak, out current, out error), current: current, error: error));
				SetInspection(tuple.Item1, tuple.Item2);
				if (tuple.Item1 == TweakState.Unreadable)
				{
					Error = tuple.Item3 ?? "Current state could not be read.";
				}
				else if (!preserveOperationError)
				{
					Error = "";
				}
			}
			finally
			{
				InspectionSlots.Release();
			}
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task RestoreSavedAsync()
	{
		if (IsLocked || IsBusy)
		{
			return;
		}
		IsBusy = true;
		try
		{
			TweakOperationResult tweakOperationResult = await Task.Run(() => _svc.RevertDetailed(Tweak));
			Error = (tweakOperationResult.Success ? "" : tweakOperationResult.Message);
			LastResult = (tweakOperationResult.Success ? tweakOperationResult.Display : "");
			ToastCenter.Show(tweakOperationResult.Display, !tweakOperationResult.Success);
		}
		finally
		{
			IsBusy = false;
			RefreshState();
		}
	}

	public void ApplyPreset(long value)
	{
		IsFlyoutOpen = false;
		if (IsLocked || IsBusy || !ConfirmRisk())
		{
			return;
		}
		try
		{
			BackupSession backupSession = new BackupSession
			{
				Description = "Windows Tweaks & Privacy: " + Title
			};
			bool flag = _svc.ApplyValue(Tweak, value, backupSession);
			backupSession.Save();
			Error = "";
			ToastCenter.Show(Title + ": " + (flag ? "requested value verified" : "already configured") + ".");
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

	private async Task ToggleAsync(bool value)
	{
		if (IsAdjustable || IsLocked || IsBusy || value == _isOn)
		{
			return;
		}
		if (!value && Tweak.IsCommandBased && MessageBox.Show("This older command did not save the original value. Switching it off resets its configured default.\n\n" + Summary + "\n\nReset this setting?", "Reset legacy setting", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
		{
			RefreshState();
			return;
		}
		if (value && !ConfirmRisk())
		{
			RefreshState();
			return;
		}
		IsBusy = true;
		try
		{
			TweakOperationResult tweakOperationResult = await Task.Run(() => ApplyOrRevertRaw(value));
			Error = (tweakOperationResult.Success ? "" : tweakOperationResult.Message);
			LastResult = (tweakOperationResult.Success ? tweakOperationResult.Display : "");
			ToastCenter.Show(tweakOperationResult.Display, !tweakOperationResult.Success);
		}
		finally
		{
			IsBusy = false;
			RefreshState();
		}
	}

	internal void ShowResult(TweakOperationResult result)
	{
		Error = (result.Success ? "" : result.Message);
		LastResult = (result.Success ? result.Display : "");
		RefreshState();
	}

	internal TweakOperationResult ApplyOrRevertRaw(bool applying)
	{
		if (!applying)
		{
			return _svc.RevertDetailed(Tweak);
		}
		BackupSession backup = new BackupSession
		{
			Description = "Windows Tweaks & Privacy: " + Title
		};
		return _svc.ApplyDetailed(Tweak, backup);
	}
}
