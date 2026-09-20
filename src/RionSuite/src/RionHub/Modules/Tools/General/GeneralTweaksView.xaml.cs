// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using RionHub.Guide;

namespace RionHub.Modules.Tools.General;

public partial class GeneralTweaksView : UserControl
{
	private static readonly DependencyPropertyKey IsCompactPropertyKey = DependencyProperty.RegisterReadOnly("IsCompact", typeof(bool), typeof(GeneralTweaksView), new PropertyMetadata(false));

	public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;

	private bool _returnPresetFocus;

	public static readonly DependencyProperty SearchQueryProperty = DependencyProperty.Register("SearchQuery", typeof(string), typeof(GeneralTweaksView), new PropertyMetadata(""));

	public bool IsCompact => (bool)GetValue(IsCompactProperty);

	public string SearchQuery
	{
		get
		{
			return (string)GetValue(SearchQueryProperty);
		}
		set
		{
			SetValue(SearchQueryProperty, value);
		}
	}

	private void OnMmcssScan(object sender, RoutedEventArgs e)
	{
		MmcssInspector.Show();
	}

    private void OpenWindowsPreference(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string uri } && uri is "ms-settings:taskbar" or "ms-settings:personalization-start" or "ms-settings:easeofaccess-visualeffects")
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Windows settings"); }
    }

	private void OnMmcssHelp(object sender, RoutedEventArgs e)
	{
		RionDocs.Show("mmcss");
	}

	public GeneralTweaksView()
	{
		InitializeComponent();
		SetBinding(SearchQueryProperty, new Binding("SearchText"));
	}

	private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
	{
		SetValue(IsCompactPropertyKey, true);
	}

	private void OnPresetOpened(object sender, EventArgs e)
	{
		Popup popup = sender as Popup;
		if (popup == null)
		{
			return;
		}
		_returnPresetFocus = false;
		popup.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
		{
			if (popup.IsOpen)
			{
				UIElement child = popup.Child;
				if (child != null)
				{
					child.UpdateLayout();
					FindFirstButton(child)?.Focus();
				}
			}
		});
	}

	private static Button? FindFirstButton(DependencyObject parent)
	{
		if (parent is Button { IsEnabled: not false } button)
		{
			return button;
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			Button button2 = FindFirstButton(VisualTreeHelper.GetChild(parent, i));
			if (button2 != null)
			{
				return button2;
			}
		}
		return null;
	}

	private void OnPresetKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape && sender is FrameworkElement { DataContext: TweakBundleRowViewModel dataContext })
		{
			_returnPresetFocus = true;
			dataContext.IsFlyoutOpen = false;
			e.Handled = true;
		}
	}

	private void OnPresetClosed(object sender, EventArgs e)
	{
		if (sender is Popup { PlacementTarget: { } placementTarget, Child: { } child } && (_returnPresetFocus || child.IsKeyboardFocusWithin || Keyboard.FocusedElement == null))
		{
			placementTarget.Focus();
		}
		_returnPresetFocus = false;
	}

	private void OnPresetChosen(object sender, RoutedEventArgs e)
	{
		_returnPresetFocus = true;
	}

}
