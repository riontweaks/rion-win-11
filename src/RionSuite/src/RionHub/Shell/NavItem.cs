// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RionHub;

public sealed class NavItem : ObservableObject
{
	private readonly Func<UserControl>? _viewFactory;

	private UserControl? _view;

	private bool _expanded;

	private string _rowTag = "";

	public string Title { get; }

	public string Glyph { get; }

	public string? TopTitle { get; set; }

	public string TopLabel => TopTitle ?? Title;

	public Geometry? IconGeometry { get; set; }

	public ObservableCollection<NavItem> Children { get; } = new ObservableCollection<NavItem>();

	public bool HasChildren => Children.Count > 0;

	public bool CascadeExpand { get; init; }

	public object? Payload { get; set; }

	public bool IsAction { get; init; }

	public bool IsExpanded
	{
		get
		{
			return _expanded;
		}
		set
		{
			if (!Set(ref _expanded, value, "IsExpanded") || !value || !CascadeExpand)
			{
				return;
			}
			foreach (NavItem child in Children)
			{
				if (child.HasChildren)
				{
					child.IsExpanded = true;
				}
			}
		}
	}

	public string RowTag
	{
		get
		{
			return _rowTag;
		}
		internal set
		{
			Set(ref _rowTag, value, "RowTag");
		}
	}

	public RelayCommand ActivateCommand { get; }

	public UserControl? View
	{
		get
		{
			if (_viewFactory == null)
			{
				return null;
			}
			if (_view != null)
			{
				return _view;
			}
			try
			{
				_view = _viewFactory();
			}
			catch (Exception value)
			{
				_view = new UserControl
				{
					Content = new TextBlock
					{
						Text = $"“{Title}” failed to load:\n\n{value}",
						Margin = new Thickness(28.0),
						TextWrapping = TextWrapping.Wrap
					}
				};
			}
			return _view;
		}
	}

	public event Action<NavItem>? Selected;

	public NavItem(string title, string glyph = "", Func<UserControl>? viewFactory = null)
	{
		Title = title;
		Glyph = glyph;
		_viewFactory = viewFactory;
		ActivateCommand = new RelayCommand(Activate);
	}

	private void Activate()
	{
		if (HasChildren)
		{
			IsExpanded = !IsExpanded;
		}
		else
		{
			this.Selected?.Invoke(this);
		}
	}
}
