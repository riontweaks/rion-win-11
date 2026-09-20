// Recovered from the original September 15 executable using ILSpy; original source unavailable.

using System;

using System.CodeDom.Compiler;

using System.Collections.Generic;

using System.ComponentModel;

using System.Diagnostics;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

using System.Windows.Markup;

using System.Windows.Media;

using System.Windows.Shapes;

using System.Windows.Threading;

using GlassKit;

using RadeonSoftwareSlimmer.Views;

using RionHub.Guide;

using RionHub.Shell;



namespace RionHub;



public partial class ShellWindow : GlassWindow

{

	private InstallMonitorWindow? _monitorWindow;



	private readonly TourState _tour;



	private IReadOnlyList<TourStep>? _steps;



	private int _stepIndex;



	public ShellWindow()

	{

		ScrollWheelRouting.Register();

		InitializeComponent();


		WindowFit.Apply(this, 1280.0, 860.0);

		base.SourceInitialized += delegate

		{

			WindowFit.Apply(this, 1280.0, 860.0);

		};

		if (base.DataContext is ShellViewModel shellViewModel)

		{

			shellViewModel.MonitorWindowRequested += ShowMonitorWindow;

		}

		base.Activated += delegate

		{

			UpdateForeground();

		};

		base.Deactivated += delegate

		{

			UpdateForeground();

		};

		base.StateChanged += delegate
		{
			UpdateForeground();
			UpdateCaptionState();
		};

		base.IsVisibleChanged += delegate

		{

			UpdateForeground();

		};

		base.Closed += delegate

		{

			if (base.DataContext is ShellViewModel shellViewModel2)

			{

				shellViewModel2.SetForeground(foreground: false);

			}

		};

		_tour = new TourState(TourNext, TourBack, TourSkip);

		TourHost.DataContext = _tour;

		Tour.Launch = StartTour;

		base.SizeChanged += delegate

		{

			if (_tour.Active)

			{

				RepositionCurrent();

			}

		};

		base.PreviewKeyDown += delegate(object _, KeyEventArgs e)

		{

			if (_tour.Active && e.Key == Key.Escape)

			{

				TourSkip();

				e.Handled = true;

			}

		};

	}



	private void UpdateForeground()

	{

		if (base.DataContext is ShellViewModel shellViewModel)

		{

			shellViewModel.SetForeground(base.IsActive && base.IsVisible && base.WindowState != WindowState.Minimized);

		}

	}



	private void Help_Click(object sender, RoutedEventArgs e)

	{

		if (base.DataContext is ShellViewModel shellViewModel)

		{

			RionDocs.ShowForPage(shellViewModel.ActiveContent, shellViewModel.HeaderTitle);

		}

	}



	private void Min_Click(object sender, RoutedEventArgs e)

	{

		Minimise();

	}



	private void Max_Click(object sender, RoutedEventArgs e)
	{
		WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
	}

	private void UpdateCaptionState()
	{
		bool maximized = WindowState == WindowState.Maximized;
		MaxGlyph.Data = Geometry.Parse(maximized ? "M1,4 H8 V11 H1 Z M4,4 V1 H11 V8 H8" : "M1,1 H11 V11 H1 Z");
		RootShell.Margin = new Thickness(maximized ? 8 : 0);
	}


	private void Close_Click(object sender, RoutedEventArgs e)

	{

		Close();

	}



	private void Tab_Click(object sender, RoutedEventArgs e)

	{

		if (sender is Button { DataContext: NavItem dataContext } && base.DataContext is ShellViewModel shellViewModel)

		{

			shellViewModel.SelectTab(dataContext);

		}

	}



	private void Discord_Click(object sender, RoutedEventArgs e)

	{

		try

		{

			Process.Start(new ProcessStartInfo("https://discord.gg/5vtgShuks3")

			{

				UseShellExecute = true

			});

		}

		catch

		{

		}

	}



	private void ShowMonitorWindow()

	{

		if (!(base.DataContext is ShellViewModel shellViewModel))

		{

			return;

		}

		if (_monitorWindow == null)

		{

			_monitorWindow = new InstallMonitorWindow(shellViewModel.Amd.Wizard.Monitor, shellViewModel.Amd.Wizard.AdvanceFromInstall)

			{

				Owner = this,

				Icon = base.Icon

			};

			_monitorWindow.Closed += delegate

			{

				_monitorWindow = null;

			};

		}

		_monitorWindow.Show();

		if (_monitorWindow.WindowState == WindowState.Minimized)

		{

			_monitorWindow.WindowState = WindowState.Normal;

		}

		_monitorWindow.Activate();

	}



	private void StartTour(string topic)

	{

		_steps = TourDefinitions.For(topic);

		_stepIndex = -1;

		_tour.Active = true;

		TourGoTo(0);

	}



	private void TourNext()

	{

		TourGoTo(_stepIndex + 1);

	}



	private void TourBack()

	{

		TourGoTo(_stepIndex - 1);

	}



	private void TourSkip()

	{

		_tour.Active = false;

		_steps = null;

	}



	private void TourGoTo(int index)

	{

		if (_steps == null)

		{

			return;

		}

		if (index < 0)

		{

			index = 0;

		}

		if (index >= _steps.Count)

		{

			TourSkip();

			return;

		}

		_stepIndex = index;

		TourStep tourStep = _steps[_stepIndex];

		if (!string.IsNullOrEmpty(tourStep.NavKey) && base.DataContext is ShellViewModel shellViewModel)

		{

			shellViewModel.NavigateToKey(tourStep.NavKey);

		}

		_tour.Heading = tourStep.Heading;

		_tour.Body = tourStep.Body;

		_tour.Progress = $"Step {_stepIndex + 1} of {_steps.Count}";

		_tour.CanBack = _stepIndex > 0;

		_tour.NextLabel = ((_stepIndex == _steps.Count - 1) ? "Done" : "Next");

		base.Dispatcher.BeginInvoke(new Action(RepositionCurrent), DispatcherPriority.Loaded);

		base.Dispatcher.BeginInvoke(new Action(RepositionCurrent), DispatcherPriority.ContextIdle);

	}



	private void RepositionCurrent()

	{

		if (_tour.Active && _steps != null && _stepIndex >= 0 && _stepIndex < _steps.Count)

		{

			Reposition(_steps[_stepIndex]);

		}

	}



	private void Reposition(TourStep step)

	{

		double actualWidth = TourHost.ActualWidth;

		double actualHeight = TourHost.ActualHeight;

		if (actualWidth <= 0.0 || actualHeight <= 0.0)

		{

			return;

		}

		RectangleGeometry rectangleGeometry = new RectangleGeometry(new Rect(0.0, 0.0, actualWidth, actualHeight));

		FrameworkElement frameworkElement = (string.IsNullOrEmpty(step.TargetId) ? null : Tour.FindById(RootShell, step.TargetId));

		if (frameworkElement == null || frameworkElement.ActualWidth <= 0.0 || frameworkElement.ActualHeight <= 0.0)

		{

			TourScrim.Data = rectangleGeometry;

			_tour.HasSpotlight = false;

			_tour.BubbleHAlign = HorizontalAlignment.Center;

			_tour.BubbleVAlign = VerticalAlignment.Center;

			_tour.BubbleMargin = new Thickness(0.0);

			return;

		}

		Rect rect;

		try

		{

			rect = frameworkElement.TransformToVisual(TourHost).TransformBounds(new Rect(0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight));

		}

		catch

		{

			TourScrim.Data = rectangleGeometry;

			_tour.HasSpotlight = false;

			return;

		}

		Rect rect2 = rect;

		rect2.Inflate(6.0, 6.0);

		rect2.Intersect(new Rect(0.0, 0.0, actualWidth, actualHeight));

		RectangleGeometry geometry = new RectangleGeometry(rect2, 8.0, 8.0);

		TourScrim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, rectangleGeometry, geometry);

		_tour.HasSpotlight = true;

		_tour.RingMargin = new Thickness(rect2.X, rect2.Y, 0.0, 0.0);

		_tour.RingWidth = rect2.Width;

		_tour.RingHeight = rect2.Height;

		double left = Math.Max(16.0, Math.Min(rect2.X, actualWidth - 380.0 - 16.0));

		double num = rect2.Bottom + 14.0;

		if (num + 240.0 > actualHeight - 16.0)

		{

			num = Math.Max(16.0, rect2.Y - 240.0 - 14.0);

		}

		_tour.BubbleHAlign = HorizontalAlignment.Left;

		_tour.BubbleVAlign = VerticalAlignment.Top;

		_tour.BubbleMargin = new Thickness(left, num, 0.0, 0.0);

	}



}



