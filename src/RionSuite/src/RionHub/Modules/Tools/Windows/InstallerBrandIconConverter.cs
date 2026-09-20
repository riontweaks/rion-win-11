using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RionHub.Modules.Tools.Windows;

public sealed class InstallerBrandIconConverter : IValueConverter
{
	private readonly ResourceDictionary icons = new ResourceDictionary
	{
		Source = new Uri("/Rion Win 11;component/Modules/Tools/Windows/InstallerBrandIcons.xaml", UriKind.Relative)
	};

	private static readonly Dictionary<string, string> bundled = new Dictionary<string, string>
	{
		["Cinebench R23"] = "Cinebench R23.png",
		["CPU-Z"] = "CPU-Z.png",
		["HWiNFO"] = "HWiNFO.png",
		["RAM Test Pro"] = "RAM Test Pro.png",
		["SMU Debug Tool"] = "SMU Debug Tool.png",
		["TestMem5 / TM5"] = "TestMem5.png",
		["Thaiphoon Burner"] = "Thaiphoon Burner.png",
		["ZenTimings"] = "ZenTimings.png",
		["Device Cleanup"] = "DeviceCleanup.png",
		["Windows Update Blocker (WUB)"] = "WindowsUpdateBlockerWUB.png",
		["NVIDIA Profile Inspector"] = "NVIDIAProfileInspector.png",
		["NVCleanstall"] = "NVCleanstall.png",
		["Autoruns"] = "Autoruns.png",
		["TCPView"] = "TCPView.png",
		["Process Explorer"] = "ProcessExplorer.png",
		["O&O ShutUp10"] = "OOShutUp10.png",
		["Display Driver Uninstaller (DDU)"] = "DisplayDriverUninstallerDDU.png"
	};

	private static readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>();

	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!(value is string text))
		{
			return null;
		}
		if (bundled.TryGetValue(text, out string value2))
		{
			if (!cache.TryGetValue(text, out ImageSource value3))
			{
				value3 = new BitmapImage(new Uri("pack://application:,,,/Rion Win 11;component/Assets/InstallerIcons/" + value2));
				value3.Freeze();
				cache[text] = value3;
			}
			return value3;
		}
		if (!icons.Contains(text))
		{
			return SemanticIcon(text);
		}
		return icons[text] as ImageSource;
	}

	private static ImageSource SemanticIcon(string name)
	{
		string text = name.ToLowerInvariant();
		string source = (text.Contains("msi mode") ? "M2,5 H22 V18 H2 Z M6,18 V22 M10,18 V22 M14,18 V22 M18,18 V22 M7,9 H17 V14 H7 Z" : (text.Contains("vc++") ? "M3,3 H21 V21 H3 Z M10,8 A5,5 0 1 0 10,16 M13,11 H19 M16,8 V14" : (text.Contains(".net") ? "M3,3 H21 V21 H3 Z M7,17 V7 L17,17 V7" : (text.Contains("directx") ? "M3,3 L21,21 M21,3 L3,21 M3,3 H21 V21 H3 Z" : (text.Contains("webview") ? "M2,3 H22 V21 H2 Z M2,8 H22 M8,12 L5,15 L8,18 M16,12 L19,15 L16,18" : (text.Contains("onedrive") ? "M6,19 H18 A4,4 0 0 0 18,11 A6,6 0 0 0 6,8 A5,5 0 0 0 6,19 Z" : (text.Contains("calculator") ? "M5,2 H19 V22 H5 Z M8,6 H16 M8,11 H10 M14,11 H16 M8,15 H10 M14,15 H16 M8,19 H10 M14,19 H16" : (text.Contains("camera") ? "M3,6 H8 L10,3 H15 L17,6 H22 V21 H3 Z M16,13 A5,5 0 1 1 6,13 A5,5 0 1 1 16,13" : ((text.Contains("photo") || text.Contains("paint")) ? "M2,3 H22 V21 H2 Z M3,18 L9,11 L14,16 L18,10 L22,15 M8,7 H9" : (text.Contains("terminal") ? "M2,4 H22 V21 H2 Z M6,9 L10,13 L6,17 M13,17 H18" : ((text.Contains("notepad") || text.Contains("journal")) ? "M5,2 H19 V22 H5 Z M8,7 H16 M8,11 H16 M8,15 H16 M8,19 H13" : (text.Contains("store") ? "M3,7 H21 V22 H3 Z M8,7 V5 A4,4 0 0 1 16,5 V7" : ((text.Contains("xbox") || text.Contains("gaming")) ? "M6,6 H18 L22,19 H17 L14,16 H10 L7,19 H2 Z M6,11 H10 M8,9 V13 M16,10 H17 M18,13 H19" : ((text.Contains("mail") || text.Contains("outlook")) ? "M2,5 H22 V20 H2 Z M2,5 L12,13 L22,5" : ((text.Contains("music") || text.Contains("sound")) ? "M9,17 V4 L20,2 V15 M9,7 L20,5 M9,17 A3,3 0 1 1 3,17 A3,3 0 1 1 9,17 M20,15 A3,3 0 1 1 14,15 A3,3 0 1 1 20,15" : ((text.Contains("video") || text.Contains("clipchamp")) ? "M2,4 H22 V21 H2 Z M9,8 L16,13 L9,18 Z" : "M3,3 H21 V21 H3 Z M3,8 H21 M7,5 H8 M11,5 H12"))))))))))))))));
		DrawingImage drawingImage = new DrawingImage(new GeometryDrawing(null, new Pen(new SolidColorBrush(Color.FromRgb(160, 191, byte.MaxValue)), 1.6), Geometry.Parse(source)));
		drawingImage.Freeze();
		return drawingImage;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
