// Recovered from the original September 15 executable using ILSpy; original source unavailable.
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using RionHub.Guide;

namespace RionHub;

public partial class App : Application
{

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
			new RoutedEventHandler((sender, _) => Theme.WindowCaption.Apply((Window)sender)));
		ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        {
            InstallGlobalExceptionHandling();
			RestorePointPrompt.Configure();
			base.MainWindow = new ShellWindow();
			base.MainWindow.Show();
			// Optional utilities are prepared only on explicit request.
			base.Dispatcher.BeginInvoke(new Action(RestorePointPrompt.Initialize), DispatcherPriority.ContextIdle);
		}
	}

	private static async Task PrepareUtilities()
	{
		try { await RionHub.Features.UtilityPackage.PrepareAsync(RionHub.Features.UtilitiesPage.Preparation.Token); }
		catch (Exception ex) { Log(ex, false); } // Utilities page retains the actionable download status.
	}

	private void InstallGlobalExceptionHandling()
	{
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs ex)
		{
			Log(ex.ExceptionObject as Exception, fatal: true);
		};
		base.DispatcherUnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs ex)
		{
			Log(ex.Exception, fatal: false);
			MessageBox.Show(ex.Exception.ToString(), "Rion Win 11 — unexpected error", MessageBoxButton.OK, MessageBoxImage.Hand);
			ex.Handled = true;
		};
		TaskScheduler.UnobservedTaskException += delegate(object? _, UnobservedTaskExceptionEventArgs ex)
		{
			Log(ex.Exception, fatal: false);
			ex.SetObserved();
		};
	}

	private static void Log(Exception? ex, bool fatal)
	{
		if (ex == null)
		{
			return;
		}
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RionWin11");
			Directory.CreateDirectory(text);
			File.AppendAllText(Path.Combine(text, "crash.log"), $"{DateTime.Now:s}  fatal={fatal}\n{ex}\n\n");
		}
		catch
		{
		}
	}

}
