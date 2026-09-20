// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GlassKit;

public class GlassWindow : Window
{
	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

	private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

	private const int DWMWCP_ROUND = 2;

	private const int DWMSBT_MICA = 2;

	private const int DWMSBT_ACRYLIC = 3;

	public bool BackdropEnabled { get; private set; }

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		nint handle = new WindowInteropHelper(this).Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}
		int value = 1;
		DwmSetWindowAttribute(handle, 20, ref value, 4);
		if (base.WindowStyle != WindowStyle.None)
		{
			SetCaptionColor(handle, 35, "Bg0");
			SetCaptionColor(handle, 36, "Text");
		}
		int value2 = 2;
		DwmSetWindowAttribute(handle, 33, ref value2, 4);
		int value3 = 3;
		int num = DwmSetWindowAttribute(handle, 38, ref value3, 4);
		if (num != 0)
		{
			value3 = 2;
			num = DwmSetWindowAttribute(handle, 38, ref value3, 4);
		}
		BackdropEnabled = num == 0;
		HwndSource hwndSource = HwndSource.FromHwnd(handle);
		if (BackdropEnabled)
		{
			if (hwndSource?.CompositionTarget != null)
			{
				hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;
			}
			return;
		}
		if (hwndSource?.CompositionTarget != null)
		{
			hwndSource.CompositionTarget.BackgroundColor = Colors.Black;
		}
		if (base.Content is FrameworkElement { Name: "RootShell" } frameworkElement && TryFindResource("ScrimSolid") is Brush background && frameworkElement is Border border)
		{
			border.Background = background;
		}
	}

	protected void Minimise()
	{
		base.WindowState = WindowState.Minimized;
	}

	protected void Close_()
	{
		Close();
	}

	protected void ToggleMaximise(Path maxGlyph = null, Border rootShell = null)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
		bool flag = base.WindowState == WindowState.Maximized;
		if (maxGlyph != null)
		{
			maxGlyph.Data = Geometry.Parse(flag ? "M1,4 H8 V11 H1 Z M4,1 H11 V8" : "M1,1 H11 V11 H1 Z");
		}
		if (rootShell != null)
		{
			rootShell.Margin = (flag ? new Thickness(8.0) : new Thickness(0.0));
		}
	}

	private void SetCaptionColor(nint hwnd, int attribute, string resource)
	{
		if (TryFindResource(resource) is SolidColorBrush { Color: var color })
		{
			int value = color.R | (color.G << 8) | (color.B << 16);
			DwmSetWindowAttribute(hwnd, attribute, ref value, 4);
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
}
