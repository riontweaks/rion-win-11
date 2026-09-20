using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlassKit;
using RadeonSoftwareSlimmer.Optimize;
using RionHub.Shell;

namespace RionHub.Modules.Tools.General;

public sealed partial class GeneralTweaksViewModel : ObservableObject
{
	private static readonly string[] Categories = RadeonSoftwareSlimmer.Services.EditionPolicy.IsFree ? new[] { "All Tweaks", "Performance", "Graphics", "MMCSS" } : new string[] { "All Tweaks", "Quality of life", "Performance", "System", "Graphics", "Security", "Network", "MMCSS" };

	private readonly List<TweakBundleRowViewModel> _all;

	private bool _isBulkBusy;

	private int _selectedFilterIndex;

	private string _searchText = "";

    private bool CanStartBatch=>!_isBulkBusy&&!_all.Any(r=>r.IsBusy);

	public ObservableCollection<string> Filters { get; }

	public ObservableCollection<object> Rows { get; }

	public int SelectedFilterIndex
	{
		get
		{
			return _selectedFilterIndex;
		}
		set
		{
			if (Set(ref _selectedFilterIndex, value, "SelectedFilterIndex"))
			{
				Recompute();
			}
		}
	}

	public string SearchText
	{
		get
		{
			return _searchText;
		}
		set
		{
			if (Set(ref _searchText, value, "SearchText"))
			{
				Recompute();
			}
		}
	}

	public bool CanEditTweaks => !_isBulkBusy;

	public ObservableCollection<TweakOperationResult> Results { get; } = new ObservableCollection<TweakOperationResult>();

	public bool HasResults => Results.Count > 0;

	public string ResultSummary => TweakOperationResult.Summarize(Results);

	public string AppliedSummary
	{
		get
		{
			int value = _all.Sum((TweakBundleRowViewModel r) => r.AppliedCount);
			int value2 = _all.Sum((TweakBundleRowViewModel r) => r.TotalCount);
			return $"{value} of {value2} settings applied across {_all.Count} cards";
		}
	}

	public RelayCommand RefreshCommand { get; }

	public RelayCommand ApplyAllCommand { get; }

	public RelayCommand ApplyAllTweaksCommand { get; }

	public RelayCommand RevertAllCommand { get; }

	public GeneralTweaksViewModel()
	{
		_all = (from b in TweakBundleCatalog.All
            where RadeonSoftwareSlimmer.Services.EditionPolicy.IncludesTweak(b.Id,b.Section)
			orderby Math.Max(0, TweakBundleCatalog.Sections.ToList().IndexOf(b.Section))
			select new TweakBundleRowViewModel(b)).ToList();
		Filters = new ObservableCollection<string>(Categories);
		Rows = new ObservableCollection<object>();
		RefreshCommand = new RelayCommand(RefreshAll);
		ApplyAllCommand = new RelayCommand(async delegate
		{
			await ApplyAllVisibleAsync();
		}, () => CanStartBatch && Rows.OfType<TweakBundleRowViewModel>().Any((TweakBundleRowViewModel r) => r.Recommended && !r.IsFullyOn));
		ApplyAllTweaksCommand = new RelayCommand(async delegate
		{
			await ApplyEntireCatalogAsync();
		}, () => CanStartBatch);
		RevertAllCommand = new RelayCommand(async delegate
		{
			await RevertAllVisibleAsync();
		}, () => CanStartBatch);
		foreach (TweakBundleRowViewModel item in _all)
		{
			item.PropertyChanged += delegate(object? _, PropertyChangedEventArgs args)
			{
				bool flag;
				switch (args.PropertyName)
				{
				case "IsBusy":
				case "IsOn":
				case "AppliedCount":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					ApplyAllCommand.RaiseCanExecuteChanged();
					ApplyAllTweaksCommand.RaiseCanExecuteChanged();
					RevertAllCommand.RaiseCanExecuteChanged();
					Raise("AppliedSummary");
				}
			};
		}
		Recompute();
	}

	private void Recompute()
	{
		string category = Categories[Math.Clamp(_selectedFilterIndex, 0, Categories.Length - 1)];
        bool allTweaks = category == "All Tweaks";
        Rows.Clear();
        foreach (var row in _all) row.UseAdvancedSection = allTweaks && TweakPresentationOrder.IsAdvanced(row.Bundle);


        var ordered = TweakPresentationOrder.Sort(_all.Select(r => r.Bundle), allTweaks)
            .Select(b => _all.First(r => ReferenceEquals(r.Bundle, b)));
        foreach (TweakBundleRowViewModel item in ordered.Where(r => r.MatchesCategory(category) && r.MatchesSearch(SearchText)))
		{
			Rows.Add(item);
		}
		Raise("AppliedSummary");
		ApplyAllCommand.RaiseCanExecuteChanged();
		ApplyAllTweaksCommand.RaiseCanExecuteChanged();
		RevertAllCommand.RaiseCanExecuteChanged();
	}

	private void RefreshAll()
	{
		foreach (TweakBundleRowViewModel item in _all)
		{
			item.Refresh();
		}
		Raise("AppliedSummary");
	}

	private async Task ApplyAllVisibleAsync()
	{
		await BulkAsync(applying: true, (from r in Rows.OfType<TweakBundleRowViewModel>()
			where r.Recommended
			select r).ToList(), "Applying recommended tweaks");
	}

	private async Task RevertAllVisibleAsync()
	{
		await BulkAsync(applying: false, (from r in Rows.OfType<TweakBundleRowViewModel>()
			where r.AppliedCount > 0 && !r.IsAdjustable
			select r).ToList(), "Restoring tweaks");
	}


	private async Task BulkAsync(bool applying, List<TweakBundleRowViewModel> targets, string verb, bool includeActions = false)
	{
        includeActions &= !RadeonSoftwareSlimmer.Services.EditionPolicy.IsFree;
		if (!CanStartBatch || targets.Count == 0 || (applying && !TweakConsent.Request(verb)))
		{
			return;
		}
		using (TweakConsent.ApprovedBatch())
		{
			_isBulkBusy = true;
			foreach (TweakBundleRowViewModel item in _all)
			{
				item.IsLocked = true;
			}
			Results.Clear();
			Raise("CanEditTweaks");
			Raise("HasResults");
			ApplyAllCommand.RaiseCanExecuteChanged();
			ApplyAllTweaksCommand.RaiseCanExecuteChanged();
			RevertAllCommand.RaiseCanExecuteChanged();
			List<TweakBatchItem> list = targets.SelectMany((TweakBundleRowViewModel r) => r.BatchItems(applying)).ToList();
			IProgressToastHandle toast = ToastCenter.ShowProgress($"{verb}… 0/{list.Count}");
			try
			{
				await TweakBatch.RunAsync(list, delegate(int done, int total, TweakOperationResult result)
				{
					Results.Add(result);
					toast.Report(done, total, $"{verb}… {done}/{total}: {result.Title}");
					Raise("HasResults");
					Raise("ResultSummary");
				});
				if (Results.Any((TweakOperationResult r) => !r.Success))
				{
					toast.Fail(ResultSummary);
				}
				else
				{
					toast.Complete(ResultSummary);
				}
			}
			catch (Exception ex)
			{
				toast.Fail("Operation interrupted: " + ex.Message + ". " + ResultSummary);
			}
			finally
			{
				_isBulkBusy = false;
				foreach (TweakBundleRowViewModel item2 in _all)
				{
					item2.IsLocked = false;
					item2.Refresh();
				}
				Raise("CanEditTweaks");
				ApplyAllCommand.RaiseCanExecuteChanged();
				ApplyAllTweaksCommand.RaiseCanExecuteChanged();
				RevertAllCommand.RaiseCanExecuteChanged();
				Raise("AppliedSummary");
			}
		}
	}
}
