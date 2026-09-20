// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using GlassKit;
using Microsoft.Win32;

namespace RionHub.Guide;

public static class MmcssInspector
{
	public static readonly string[] TaskNames = new string[7] { "Games", "Audio", "Capture", "Distribution", "Playback", "Pro Audio", "Window Manager" };

	private static readonly string[] Fields = new string[6] { "Priority", "BackgroundPriority", "Scheduling Category", "Background Only", "Affinity", "Clock Rate" };

	private const string Profile = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile";

	public static MmcssFinding Assess(MmcssTaskReading reading)
	{
		if (reading.Error.Length > 0)
		{
			return new MmcssFinding(reading.Task, "Could not read current settings", reading.Error);
		}
		if (!reading.Present)
		{
			return new MmcssFinding(reading.Task, "Task key is absent", "Leave absent unless the app or device vendor requires this task. Rion will not create it from a guessed preset.");
		}
		List<string> list = new List<string>();
		string[] array = new string[2] { "Priority", "BackgroundPriority" };
		foreach (string text in array)
		{
			object obj = Value(text);
			if (obj != null && (!(obj is int num) || num < 1 || num > 8))
			{
				list.Add(text + " is outside the documented DWORD range 1–8.");
			}
		}
		object obj2 = Value("Scheduling Category");
		bool flag = obj2 != null;
		if (flag)
		{
			bool flag2;
			switch (obj2 as string)
			{
			case "Low":
			case "Medium":
			case "High":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = !flag2;
		}
		if (flag)
		{
			list.Add("Scheduling Category should be Low, Medium or High.");
		}
		object obj3 = Value("Background Only");
		flag = obj3 != null;
		if (flag)
		{
			bool flag2;
			switch (obj3 as string)
			{
			case "True":
			case "False":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = !flag2;
		}
		if (flag)
		{
			list.Add("Background Only should be the text True or False.");
		}
		if (Value("Scheduling Category") is string text2 && text2 == "High")
		{
			list.Add("High treats Priority as 2 regardless of the displayed number; it is intended for Pro Audio.");
		}
		if (Value("Affinity") is int num2 && num2 != 0 && num2 != -1)
		{
			list.Add("A processor affinity mask is set. Confirm an application-specific reason before limiting the available CPUs.");
		}
		string current = string.Join(" · ", Fields.Select((string f) => f + ": " + (Value(f)?.ToString() ?? "not set")));
		string advice = ((list.Count == 0) ? "Keep the current configuration. No value check identified a reason to change this task." : (string.Join(" ", list) + " Review saved changes or the application's guidance before adjusting."));
		return new MmcssFinding(reading.Task, current, advice);
		object? Value(string key)
		{
			return reading.Values.GetValueOrDefault(key);
		}
	}

	public static MmcssFinding[] Scan()
	{
		RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
		try
		{
			return TaskNames.Select(delegate(string task)
			{
				try
				{
					RegistryKey key = machine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\" + task);
					try
					{
						return Assess(new MmcssTaskReading(task, key != null, Fields.ToDictionary((string f) => f, (string f) => key?.GetValue(f))));
					}
					finally
					{
						if (key != null)
						{
							((IDisposable)key).Dispose();
						}
					}
				}
				catch (Exception ex)
				{
					return Assess(new MmcssTaskReading(task, Present: false, new Dictionary<string, object>(), ex.Message));
				}
			}).ToArray();
		}
		finally
		{
			if (machine != null)
			{
				((IDisposable)machine).Dispose();
			}
		}
	}

	public static void Show()
	{
		GlassWindow glassWindow = new GlassWindow
		{
			Title = "MMCSS · Check my settings",
			Width = 700.0,
			Height = 700.0,
			MinWidth = 340.0,
			MinHeight = 300.0,
			MaxHeight = SystemParameters.WorkArea.Height,
			MaxWidth = SystemParameters.WorkArea.Width,
			Owner = Application.Current.MainWindow,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = (Brush)Application.Current.FindResource("Bg0"),
			Foreground = (Brush)Application.Current.FindResource("Text"),
			FontFamily = (FontFamily)Application.Current.FindResource("AppFont"),
			FontSize = 14.0
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		stackPanel.Children.Add(Text("MMCSS settings", 24.0));
		stackPanel.Children.Add(Text("Task priority applies to threads that an app registers with MMCSS. It does not set every process to High priority."));
		ComboBox use = new ComboBox
		{
			ItemsSource = new string[3] { "Gaming", "Recording and streaming", "Audio production" },
			SelectedIndex = 0,
			MinHeight = 36.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		AutomationProperties.SetName(use, "MMCSS use case");
		TextBlock guidance = Text("");
		use.SelectionChanged += delegate
		{
			UpdateGuidance();
		};
		UpdateGuidance();
		stackPanel.Children.Add(use);
		stackPanel.Children.Add(guidance);
		stackPanel.Children.Add(Text("To adjust: choose a task card, select one value, then Apply value. Priority is 1–8; High category treats it as 2. Clock Rate is a scheduling hint, not a guaranteed timer resolution."));
		TextBlock summary = Text("Reading task settings…");
		stackPanel.Children.Add(summary);
		StackPanel tasks = new StackPanel();
		stackPanel.Children.Add(tasks);
		Button button = new Button
		{
			Content = "Read the MMCSS guide",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		button.Click += delegate
		{
			RionDocs.Show("mmcss");
		};
		stackPanel.Children.Add(button);
		glassWindow.Content = new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		glassWindow.Loaded += async delegate
		{
			try
			{
				MmcssFinding[] obj = await Task.Run((Func<MmcssFinding[]>)Scan);
				summary.Visibility = Visibility.Collapsed;
				MmcssFinding[] array = obj;
				foreach (MmcssFinding mmcssFinding in array)
				{
					StackPanel stackPanel2 = new StackPanel
					{
						Margin = new Thickness(10.0)
					};
					stackPanel2.Children.Add(Text(mmcssFinding.Current, 12.0));
					stackPanel2.Children.Add(Text(mmcssFinding.Advice));
					tasks.Children.Add(new Expander
					{
						Header = mmcssFinding.Task,
						Content = stackPanel2,
						Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
					});
				}
			}
			catch (Exception ex)
			{
				summary.Text = "The read-only check could not finish: " + ex.Message;
			}
		};
		glassWindow.Show();
		static TextBlock Text(string text, double size = 14.0)
		{
			return new TextBlock
			{
				Text = text,
				TextWrapping = TextWrapping.Wrap,
				FontSize = size,
				FontFamily = (FontFamily)Application.Current.FindResource(size >= 18 ? "DisplayFont" : "AppFont"),
				FontWeight = size >= 18 ? FontWeights.Bold : FontWeights.Normal,
				Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
			};
		}
		void UpdateGuidance()
		{
			TextBlock textBlock = guidance;
			textBlock.Text = use.SelectedIndex switch
			{
				1 => "Start with the existing Capture, Distribution, Playback and Audio settings. Their task names describe work, not guaranteed registration by every recording app.", 
				2 => "Start with the audio application's own buffer and driver setup. Pro Audio's High category is designed for time-sensitive audio; raising every category can compete with that work.", 
				_ => "Gaming starting point: keep Windows and application task settings. There is no documented CPU/GPU-based preset that maximizes FPS across these categories.", 
			};
		}
	}
}
