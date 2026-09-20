// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlassKit;

using RadeonSoftwareSlimmer.Optimize;
using RionHub.Shell;

namespace RionHub.Guide;

public static class RestorePointPrompt
{
	private sealed record FirstUse(DateTimeOffset FirstSeenUtc, bool PromptShown);

	private static FirstUse? firstUse;

	private static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RionWin11", "Recovery", Environment.MachineName + ".json");

	public static Func<Task<RestoreInventory>> Inspect { get; set; } = ReadInventoryAsync;

	public static Func<string, Task<bool>> Create { get; set; } = CreateAsync;

	public static void Configure()
	{
		TweakConsent.BeforeApply = (string operation) => Application.Current.Dispatcher.Invoke(() => Show(operation));
		
	}

	public static void Initialize()
	{
		try
		{
			firstUse = (File.Exists(StatePath) ? JsonSerializer.Deserialize<FirstUse>(File.ReadAllText(StatePath)) : null);
			if ((object)firstUse == null)
			{
				firstUse = new FirstUse(DateTimeOffset.UtcNow, PromptShown: false);
			}
			Save();
			if (!firstUse.PromptShown)
			{
				Show("Before your first changes", onboarding: true);
				firstUse = firstUse with
				{
					PromptShown = true
				};
				Save();
			}
		}
		catch (Exception ex)
		{
			ToastCenter.Show("Recovery reminder could not be saved: " + ex.Message, isError: true);
		}
	}

	private static void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
		string text = StatePath + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(firstUse));
		File.Move(text, StatePath, overwrite: true);
	}

	public static bool HasPointSince(RestoreInventory inventory, DateTimeOffset firstSeen)
	{
		if (inventory.Known)
		{
			return inventory.Points.Any((RestorePointInfo p) => p.CreatedUtc >= firstSeen);
		}
		return false;
	}

	public static bool Show(string operation, bool onboarding = false)
	{
		Window owner = Application.Current.Windows.OfType<Window>().FirstOrDefault((Window w) => w.IsActive) ?? Application.Current.MainWindow;
		GlassWindow dialog = new GlassWindow
		{
			Title = "Create a restore point",
			Width = 580.0,
			SizeToContent = SizeToContent.Height,
			MaxWidth = SystemParameters.WorkArea.Width,
			MaxHeight = SystemParameters.WorkArea.Height,
			Owner = owner,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = (Brush)Application.Current.FindResource("Bg0"),
			Foreground = (Brush)Application.Current.FindResource("Text"),
			FontFamily = (FontFamily)Application.Current.FindResource("AppFont"),
			FontSize = 14.0,
			ResizeMode = ResizeMode.NoResize
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "Create a recovery point",
			FontSize = 24.0,
			FontFamily = (FontFamily)Application.Current.FindResource("DisplayFont"),
			FontWeight = FontWeights.Bold,
			TextWrapping = TextWrapping.Wrap
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = operation,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 8.0, 0.0, 12.0)
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "A Windows restore point gives you another way back after system changes. Rion also keeps its own change history.",
			TextWrapping = TextWrapping.Wrap
		});
		TextBlock status = new TextBlock
		{
			Text = "Checking Windows restore points…",
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 14.0, 0.0, 10.0)
		};
		stackPanel.Children.Add(status);
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Button create = new Button
		{
			Content = (onboarding ? "Create restore point" : "Create and continue"),
			IsEnabled = false,
			Margin = new Thickness(0.0, 0.0, 8.0, 8.0)
		};
		Button skip = new Button
		{
			Content = (onboarding ? "Later" : "Continue without creating"),
			Margin = new Thickness(0.0, 0.0, 8.0, 8.0)
		};
		Button cancel = new Button
		{
			Content = "Cancel",
			IsCancel = true,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Visibility = (onboarding ? Visibility.Collapsed : Visibility.Visible)
		};
		wrapPanel.Children.Add(create);
		wrapPanel.Children.Add(skip);
		wrapPanel.Children.Add(cancel);
		stackPanel.Children.Add(wrapPanel);
		Button protection = new Button
		{
			Content = "Open System Protection",
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		protection.Click += delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SystemPropertiesProtection.exe"))
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		};
		stackPanel.Children.Add(protection);
		dialog.Content = new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		skip.Click += delegate
		{
			dialog.DialogResult = true;
		};
		cancel.Click += delegate
		{
			dialog.DialogResult = false;
		};
		bool running = false;
		dialog.Closing += delegate(object? _, CancelEventArgs e)
		{
			if (running)
			{
				e.Cancel = true;
			}
		};
		dialog.Loaded += async delegate
		{
			RestoreInventory restoreInventory;
			try
			{
				restoreInventory = await Inspect();
			}
			catch (Exception ex)
			{
				restoreInventory = new RestoreInventory(Known: false, Array.Empty<RestorePointInfo>(), ex.Message);
			}
			if (dialog.IsVisible)
			{
				RestorePointInfo restorePointInfo = restoreInventory.Points.OrderByDescending((RestorePointInfo p) => p.CreatedUtc).FirstOrDefault();
				if (!onboarding && restoreInventory.Known && restorePointInfo != null)
				{
					skip.Content = "Continue with existing point";
				}
				status.Text = ((!restoreInventory.Known) ? ("Windows restore points could not be checked. " + restoreInventory.Error) : ((restorePointInfo == null) ? "No restore point was found. System Protection may need to be enabled." : ("Latest point: " + restorePointInfo.CreatedUtc.ToLocalTime().ToString("g") + " · " + restorePointInfo.Description + ((firstUse != null && HasPointSince(restoreInventory, firstUse.FirstSeenUtc)) ? "\nWindows has a point from since Rion's first use on this PC." : ""))));
				create.IsEnabled = true;
			}
		};
		create.Click += async delegate
		{
			running = true;
			Button button = create;
			Button button2 = skip;
			Button button3 = cancel;
			bool flag = (protection.IsEnabled = false);
			bool flag3 = (button3.IsEnabled = flag);
			bool isEnabled = (button2.IsEnabled = flag3);
			button.IsEnabled = isEnabled;
			status.Text = "Creating and verifying the restore point…";
			bool verified = false;
			try
			{
				verified = await Create("Rion " + Guid.NewGuid().ToString("N"));
			}
			catch (Exception ex)
			{
				status.Text = "Could not create a restore point: " + ex.Message;
			}
			finally
			{
				running = false;
				Button button4 = create;
				Button button5 = skip;
				Button button6 = cancel;
				flag = (protection.IsEnabled = true);
				flag3 = (button6.IsEnabled = flag);
				isEnabled = (button5.IsEnabled = flag3);
				button4.IsEnabled = isEnabled;
			}
			if (verified)
			{
				dialog.DialogResult = true;
			}
			else
			{
				status.Text = "A new restore point was not verified. Windows can limit creation to one per 24 hours. Check System Protection, retry, continue without creating, or cancel.";
			}
		};
		return dialog.ShowDialog() == true;
	}

	private static Task<ShellRunner.Result> Run(string script, int timeout)
	{
		return ShellRunner.RunAsync(ShellRunner.PowerShellPath, "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)), null, timeout);
	}

	public static async Task<RestoreInventory> ReadInventoryAsync()
	{
		try
		{
			ShellRunner.Result result = await Run("$ErrorActionPreference='Stop'; try { $p=@(Get-CimInstance -Namespace root/default -ClassName SystemRestore | ForEach-Object { [pscustomobject]@{Sequence=[long]$_.SequenceNumber;Description=$_.Description;CreatedUtc=[Management.ManagementDateTimeConverter]::ToDateTime($_.CreationTime).ToUniversalTime().ToString('o')} }); ConvertTo-Json -InputObject $p -Compress; exit 0 } catch { Write-Output $_.Exception.Message; exit 1 }", 30000);
			return (result.ExitCode == 0 && !result.TimedOut) ? new RestoreInventory(Known: true, JsonSerializer.Deserialize<RestorePointInfo[]>(result.Output.Trim()) ?? Array.Empty<RestorePointInfo>(), "") : new RestoreInventory(Known: false, Array.Empty<RestorePointInfo>(), result.TimedOut ? "The check timed out." : result.Output.Trim());
		}
		catch (Exception ex)
		{
			return new RestoreInventory(Known: false, Array.Empty<RestorePointInfo>(), ex.Message);
		}
	}

	private static async Task<bool> CreateAsync(string description)
	{
		ShellRunner.Result result = await Run("$ErrorActionPreference='Stop'; $WarningPreference='Stop'; try { Checkpoint-Computer -Description '" + description.Replace("'", "''") + "' -RestorePointType MODIFY_SETTINGS; exit 0 } catch { Write-Output $_.Exception.Message; exit 1 }", 120000);
		if (result.ExitCode != 0 || result.TimedOut)
		{
			return false;
		}
		RestoreInventory restoreInventory = await Inspect();
		return restoreInventory.Known && restoreInventory.Points.Any((RestorePointInfo p) => p.Description == description);
	}
}
