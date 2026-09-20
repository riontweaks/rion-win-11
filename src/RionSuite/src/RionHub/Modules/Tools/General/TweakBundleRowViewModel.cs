// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RionHub.Shell;

namespace RionHub.Modules.Tools.General;

public sealed class TweakBundleRowViewModel : ObservableObject
{
	private readonly List<TweakRowViewModel> _rows;

	private bool _isLocked;

	private bool _isBusy;

	private string _error = "";

	private string _lastResult = "";

	private bool _isFlyoutOpen;

	private RelayCommand? _toggleFlyoutCommand;

	public TweakBundle Bundle { get; }

	public string Title => Bundle.Title;

	public string Description => Bundle.Description;
    public bool IsQualityOfLife => Bundle.Id == "bundle-qol";
    public RelayCommand ApplyQualityOfLifeCommand => new RelayCommand(async () =>
    {
        if (CanToggle && MessageBox.Show("Apply supported Quality of Life preferences?\n\nThis changes appearance, Start and taskbar, OneDrive sync and supported Paint/legacy Copilot policies. OneDrive syncing and online-only access stop. Recall and startup apps require their individual actions. Original settings are saved for Restore.",
            "Review Quality of Life", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            await RunAsync(true);
    }, () => CanToggle);

	public string Section => Bundle.Section;
	public bool UseAdvancedSection { get; set; }
	public string DisplaySection => UseAdvancedSection ? TweakPresentationOrder.AdvancedSection : Section;

	public string TradeOff => Bundle.TradeOff;

	public bool IsFeatured => Bundle.IsFeatured;

	public bool RebootRequired => Bundle.RebootRequired;

	public bool IsAdjustable => Bundle.IsAdjustable;

	public bool HasMemberAdjustments => Bundle.HasMemberAdjustments;

	public bool IsSingleAdjustable
	{
		get
		{
			if (IsAdjustable)
			{
				return !HasMemberAdjustments;
			}
			return false;
		}
	}

	public bool ApplyBlocked => Bundle.ApplyBlocked;

	public string ApplyBlockedReason => Bundle.ApplyBlockedReason ?? "";

	public ObservableCollection<TweakMemberViewModel> Members { get; }

	public ObservableCollection<PresetOptionViewModel>? Presets { get; }

	public IReadOnlyList<TweakRowViewModel> Rows => _rows;

	public bool HighRisk
	{
		get
		{
			if (!Bundle.SecuritySensitive)
			{
				return _rows.Any((TweakRowViewModel r) => r.HighRisk);
			}
			return true;
		}
	}

	public bool Recommended
	{
		get
		{
			if (_rows.Count > 0)
			{
				return _rows.All((TweakRowViewModel r) => r.Recommended);
			}
			return false;
		}
	}

	public bool HasSchedulingGuide => Bundle.Members.Any(m => m.Id == "priority-separation" || m.Id.StartsWith("mmcss-", StringComparison.Ordinal));
	public RelayCommand SchedulingGuideCommand => new(() => RionHub.Guide.RionDocs.Show(Bundle.Members.Any(m => m.Id == "priority-separation") ? "priority-separation" : "mmcss"));

	public bool Experimental => _rows.Any(r => CatalogBatchPlan.Experimental(r.Tweak));

	public bool HasPerformanceWarning
	{
		get
		{
			if (!ApplyBlocked)
			{
				return !string.IsNullOrWhiteSpace(PerformanceWarning);
			}
			return false;
		}
	}

	public string PerformanceWarning => IsQualityOfLife ? "Some preferences require sign-out or restart. Review syncing and AI choices before applying." : string.Join(" ", (from r in _rows
		select r.TradeOff into t
		where !string.IsNullOrWhiteSpace(t) && !t.StartsWith("None", StringComparison.OrdinalIgnoreCase)
		select t).Distinct<string>(StringComparer.Ordinal));

	public bool CanBulkApply
	{
		get
		{
			if (!Bundle.IndividualApplyOnly && !_rows.Any((TweakRowViewModel r) => r.Tweak.ManualOnly) && !IsAdjustable && !HighRisk)
			{
				return !ApplyBlocked;
			}
			return false;
		}
	}

	public int AppliedCount => _rows.Count((TweakRowViewModel r) => r.IsOn);

	public int TotalCount => _rows.Count;

	public string CountLabel
	{
		get
		{
			if (TotalCount != 1)
			{
				if (AppliedCount > 0 && AppliedCount < TotalCount)
				{
					return $"{AppliedCount} of {TotalCount} applied";
				}
				return $"{TotalCount} settings";
			}
			return "1 setting";
		}
	}

	public string StatePill
	{
		get
		{
			if (!ApplyBlocked)
			{
				if (AppliedCount != 0)
				{
					if (AppliedCount != TotalCount)
					{
						return "Partial";
					}
					return "On";
				}
				return "Off";
			}
			return "Read only";
		}
	}

	public bool IsPartial
	{
		get
		{
			if (!ApplyBlocked && AppliedCount > 0)
			{
				return AppliedCount < TotalCount;
			}
			return false;
		}
	}

	public bool IsFullyOn
	{
		get
		{
			if (AppliedCount == TotalCount)
			{
				return TotalCount > 0;
			}
			return false;
		}
	}

	public bool ShowSwitch
	{
		get
		{
            if (IsQualityOfLife) return false;
			if (!IsAdjustable)
			{
				return !ApplyBlocked;
			}
			return false;
		}
	}

	public bool ShowRestore
	{
		get
		{
            if (IsQualityOfLife) return true;
			if (!IsAdjustable && !ApplyBlocked && !IsPartial)
			{
				return !string.IsNullOrEmpty(Error);
			}
			return true;
		}
	}

	public string CurrentValueLabel
	{
		get
		{
			if (!IsSingleAdjustable)
			{
				return "";
			}
			return _rows[0].CurrentValueLabel;
		}
	}

	public bool CanToggle
	{
		get
		{
			if (!ApplyBlocked && !IsLocked)
			{
				return !IsBusy;
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
			foreach (TweakRowViewModel row in _rows)
			{
				row.IsLocked = value || IsBusy;
			}
			Raise("CanToggle");
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
			foreach (TweakRowViewModel row in _rows)
			{
				row.IsLocked = value || IsLocked;
			}
			Raise("CanToggle");
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

	public RelayCommand InspectCommand => new RelayCommand(async delegate
	{
		await InspectAsync();
	});

	public RelayCommand RestoreSavedCommand => new RelayCommand(async delegate
	{
		await RunAsync(applying: false);
	});

	public bool IsOn
	{
		get
		{
			return IsFullyOn;
		}
		set
		{
			ToggleAsync(value);
		}
	}

	public TweakBundleRowViewModel(TweakBundle bundle)
	{
		Bundle = bundle;
		_rows = bundle.Members.Select((SystemTweak m) => new TweakRowViewModel(m)).ToList();
		Members = new ObservableCollection<TweakMemberViewModel>(_rows.Select((TweakRowViewModel r) => new TweakMemberViewModel(r)));
		if (bundle.IsAdjustable && !bundle.HasMemberAdjustments)
		{
			Presets = new ObservableCollection<PresetOptionViewModel>(bundle.Presets.Select((TweakPreset p) => new PresetOptionViewModel(p, _rows[0])));
		}
		foreach (TweakRowViewModel row in _rows)
		{
			row.PropertyChanged += delegate(object? _, PropertyChangedEventArgs args)
			{
				bool flag;
				switch (args.PropertyName)
				{
				case "IsOn":
				case "StateLabel":
				case "CurrentValueLabel":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					RaiseState();
				}
			};
		}
		RaiseState();
	}

	private void RaiseState()
	{
		Raise("AppliedCount");
		Raise("StatePill");
		Raise("IsPartial");
		Raise("ShowRestore");
		Raise("IsFullyOn");
		Raise("IsOn");
		Raise("CurrentValueLabel");
	}

	private bool ConfirmRisk(bool applying)
	{
		if (!applying || !HighRisk)
		{
			return true;
		}
		return MessageBox.Show(Title + "\n\n" + TradeOff + "\n\nApply " + CountLabel + "?", "Review risky tweak", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes;
	}

	private bool ConfirmLegacyReset(bool applying)
	{
		if (applying)
		{
			return true;
		}
		List<TweakRowViewModel> list = _rows.Where((TweakRowViewModel r) => r.Tweak.IsCommandBased && r.IsOn).ToList();
		if (list.Count == 0)
		{
			return true;
		}
		return MessageBox.Show("These older commands did not save their original values. Switching off resets their configured defaults.\n\n" + string.Join("\n", list.Select((TweakRowViewModel r) => "• " + r.Title)) + "\n\nReset them?", "Reset legacy settings", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
	}

	private async Task ToggleAsync(bool value)
	{
		if (!IsAdjustable && CanToggle && value != IsFullyOn)
		{
			if (!ConfirmRisk(value) || !ConfirmLegacyReset(value))
			{
				Refresh();
			}
			else
			{
				await RunAsync(value);
			}
		}
	}

	private async Task RunAsync(bool applying)
	{
		if (IsLocked || IsBusy || (applying && ApplyBlocked))
		{
			return;
		}
		IsBusy = true;
		try
		{
			IReadOnlyList<TweakOperationResult> readOnlyList = await Task.Run(() => ExecuteGroup(applying));
			foreach (TweakRowViewModel row in _rows)
			{
				row.RefreshState();
			}
			List<TweakOperationResult> list = readOnlyList.Where((TweakOperationResult r) => !r.Success).ToList();
			Error = ((list.Count == 0) ? "" : string.Join(" ", list.Select((TweakOperationResult r) => r.Title + ": " + r.Message)));
			LastResult = TweakOperationResult.Summarize(readOnlyList);
            if (IsQualityOfLife && applying)
                LastResult += $" {Math.Max(0, _rows.Count - readOnlyList.Count)} unsupported or individual-choice settings left unchanged.";
			ToastCenter.Show(Title + " — " + LastResult, list.Count > 0);
		}
		finally
		{
			IsBusy = false;
			Refresh();
		}
	}

	private async Task InspectAsync()
	{
		if (IsBusy)
		{
			return;
		}
		IsBusy = true;
		try
		{
			await Task.Run((Action)Refresh);
		}
		finally
		{
			IsBusy = false;
		}
	}

	public void Refresh()
	{
		foreach (TweakRowViewModel row in _rows)
		{
			row.RefreshState();
		}
		RaiseState();
	}

	private IReadOnlyList<TweakOperationResult> ExecuteGroup(bool applying)
	{
        var members = _rows.Select(r => r.Tweak).ToList();
        if (IsQualityOfLife && applying)
            members = members.Where(t => !t.IndividualApplyOnly && string.IsNullOrEmpty(t.ApplyBlockedReason)
                && string.IsNullOrEmpty(t.CheckApplySupport?.Invoke(new WindowsRegistry()))).ToList();
		return new TweakService(new WindowsRegistry()).RunGroup(members, applying);
	}

	internal IEnumerable<TweakBatchItem> BatchItems(bool applying)
	{
		if (Bundle.IndividualApplyOnly)
		{
			yield break;
		}
		yield return new TweakBatchItem(Bundle.Id, Title, async delegate
		{
			IReadOnlyList<TweakOperationResult> readOnlyList = await Task.Run(() => ExecuteGroup(applying));
			bool flag = readOnlyList.All((TweakOperationResult r) => r.Success);
			return TweakOperationResult.ForAction(Bundle.Id, Title, flag, flag ? ("Every setting verified. " + TweakOperationResult.Summarize(readOnlyList)) : string.Join(" ", from r in readOnlyList
				where !r.Success
				select r.Title + ": " + r.Message), readOnlyList.Any(delegate(TweakOperationResult r)
			{
				TweakOperationStatus status = r.Status;
				return (status == TweakOperationStatus.Applied || status == TweakOperationStatus.Restored) ? true : false;
			}));
		});
	}

	public bool MatchesCategory(string categoryFilter)
	{
		if (!(categoryFilter == "All Tweaks") && !(Bundle.Filter == categoryFilter))
		{
			if (categoryFilter == "Network")
			{
				return Bundle.Section == "Network & internet";
			}
			return false;
		}
		return true;
	}

	public bool MatchesSearch(string search)
	{
		if (!string.IsNullOrWhiteSpace(search) && !Title.Contains(search, StringComparison.OrdinalIgnoreCase) && !Description.Contains(search, StringComparison.OrdinalIgnoreCase) && !Section.Contains(search, StringComparison.OrdinalIgnoreCase))
		{
			return _rows.Any((TweakRowViewModel r) => r.MatchesSearch(search));
		}
		return true;
	}
}
