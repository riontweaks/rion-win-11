// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlassKit;
using RionHub.Shell;

namespace RionHub.Guide;

public static class RionDocs
{
	public sealed record Section(string Heading, string[] Paragraphs);

	public sealed record Source(string Label, string Url);

	public sealed record Guide(string Id, string Title, string Summary, Section[] Sections, Source[] Sources, string? GoogleDocUrl);

	private static readonly Lazy<Guide[]> guides = new Lazy<Guide[]>(delegate
	{
		using Stream utf8Json = Assembly.GetExecutingAssembly().GetManifestResourceStream("RionHub.Guide.guides.json") ?? throw new InvalidOperationException("The included guides could not be loaded.");
		return JsonSerializer.Deserialize<Guide[]>(utf8Json, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? Array.Empty<Guide>();
	});

	public static IReadOnlyList<Guide> All => guides.Value;

	public static string TopicForPage(object? page, string title)
	{
		string text = page?.GetType().FullName ?? "";
		if (text.Contains("PowerSettingsExplorer"))
		{
			return "power";
		}
		if (text.Contains(".Network."))
		{
			return "network";
		}
		if (text.Contains(".Debloat."))
		{
			return "apps";
		}
		if (text.Contains(".Windows."))
		{
			return "apps";
		}
		if (text.Contains("Adrenaline") || text.Contains("NvidiaSection"))
		{
			return "gpu-settings";
		}
		bool flag = text.Contains("Wizard") || text.Contains("Merged") || text.Contains("DriverTool");
		if (!flag)
		{
			bool flag2 = ((title == "Welcome" || title == "Install and Verify") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return "drivers";
		}
		return "tweaks";
	}

	public static void ShowForPage(object? page, string title)
	{
		Show(TopicForPage(page, title));
	}

	public static void Show(string id)
	{
		try
		{
			Guide guide = All.First((Guide g) => g.Id == id);
			Window window = Application.Current.Windows.OfType<Window>().FirstOrDefault((Window w) => w.IsActive);
			GlassWindow window2 = new GlassWindow
			{
				Title = guide.Title + " — Rion Docs",
				Width = 700.0,
				Height = 720.0,
				MaxWidth = SystemParameters.WorkArea.Width,
				MaxHeight = SystemParameters.WorkArea.Height,
				MinWidth = 320.0,
				MinHeight = 300.0,
				Owner = window,
				WindowStartupLocation = ((window == null) ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner),
				Background = Brush("Bg0"),
				Foreground = Brush("Text"),
				FontFamily = (FontFamily)Application.Current.FindResource("AppFont"),
				FontSize = 14.0,
				UseLayoutRounding = true
			};
			StackPanel stackPanel = new StackPanel
			{
				Margin = new Thickness(24.0)
			};
			stackPanel.Children.Add(Text(guide.Title, 25.0, bold: true));
			TextBlock textBlock = Text("by Rion", 14.0, bold: true);
			textBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 65, 65));
			stackPanel.Children.Add(textBlock);
			stackPanel.Children.Add(Text(string.IsNullOrEmpty(guide.GoogleDocUrl) ? "Source: Rion Docs · included guide" : "Source: Rion Docs · Google Docs", 12.0));
			stackPanel.Children.Add(Text(guide.Summary));
			if (ValidGoogleDoc(guide.GoogleDocUrl))
			{
				Button button = new Button
				{
					Content = "Open Google Doc",
					HorizontalAlignment = HorizontalAlignment.Left,
					Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
				};
				button.Click += delegate
				{
					OpenLink(guide.GoogleDocUrl);
				};
				stackPanel.Children.Add(button);
			}
			Section[] sections = guide.Sections;
			foreach (Section obj in sections)
			{
				TextBlock textBlock2 = Text(obj.Heading, 18.0, bold: true);
				textBlock2.Margin = new Thickness(0.0, 24.0, 0.0, 4.0);
				stackPanel.Children.Add(textBlock2);
				string[] paragraphs = obj.Paragraphs;
				foreach (string value in paragraphs)
				{
					stackPanel.Children.Add(Text(value));
				}
			}
			stackPanel.Children.Add(Text("Sources", 18.0, bold: true));
			Source[] sources = guide.Sources;
			foreach (Source source in sources)
			{
				Button button2 = new Button
				{
					Content = new TextBlock
					{
						Text = source.Label,
						TextWrapping = TextWrapping.Wrap
					},
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
					ToolTip = source.Url
				};
				button2.Click += delegate
				{
					OpenLink(source.Url);
				};
				stackPanel.Children.Add(button2);
			}
			Button button3 = new Button
			{
				Content = "Close guide",
				IsCancel = true,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(0.0, 24.0, 0.0, 0.0)
			};
			button3.Click += delegate
			{
				window2.Close();
			};
			stackPanel.Children.Add(button3);
			window2.PreviewKeyDown += delegate(object _, KeyEventArgs e)
			{
				if (e.Key == Key.Escape)
				{
					window2.Close();
				}
			};
			window2.Content = new ScrollViewer
			{
				Content = stackPanel,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
			};
			window2.Show();
		}
		catch (Exception ex)
		{
			ToastCenter.Show("Could not open the guide: " + ex.Message, isError: true);
		}
	}

	public static bool ValidGoogleDoc(string? url)
	{
		if (Uri.TryCreate(url, UriKind.Absolute, out Uri result) && result.Scheme == Uri.UriSchemeHttps && result.Host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase) && result.AbsolutePath.StartsWith("/document/d/", StringComparison.Ordinal))
		{
			return string.IsNullOrEmpty(result.UserInfo);
		}
		return false;
	}

	private static Brush Brush(string key)
	{
		return (Brush)Application.Current.FindResource(key);
	}

	private static TextBlock Text(string value, double size = 14.0, bool bold = false)
	{
		return new TextBlock
		{
			Text = value,
			FontSize = size,
			FontFamily = (FontFamily)Application.Current.FindResource(size >= 18 ? "DisplayFont" : "AppFont"),
			FontWeight = size >= 18 ? FontWeights.Bold : (bold ? FontWeights.SemiBold : FontWeights.Normal),
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
	}

	private static void OpenLink(string url)
	{
		try
		{
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result) || result.Scheme != Uri.UriSchemeHttps)
			{
				throw new InvalidOperationException("Unsupported guide link.");
			}
			Process.Start(new ProcessStartInfo(result.AbsoluteUri)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			ToastCenter.Show("Could not open the source: " + ex.Message, isError: true);
		}
	}
}
