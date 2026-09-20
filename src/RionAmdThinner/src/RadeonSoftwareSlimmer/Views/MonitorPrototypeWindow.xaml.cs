using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Views
{
    /// <summary>
    /// PROTOTYPE ONLY. Standalone test window for the new black-glass visual language. Launched from
    /// MainWindow's "Generate Test Window" button; shares no state with the real installation flow.
    /// </summary>
    public partial class MonitorPrototypeWindow : Window
    {
        private readonly MonitorPrototypeViewModel _vm = new MonitorPrototypeViewModel();
        private readonly TranslateTransform _heroShift = new TranslateTransform(0, 0);
        private Storyboard _shimmer;
        private Storyboard _pulse;
        private bool _acrylicOn;

        public MonitorPrototypeWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            Hero.RenderTransform = _heroShift;
            _vm.PropertyChanged += OnVmChanged;
            Loaded += (s, e) =>
            {
                ApplyStatus(_vm.Status);
                UpdateProgressVisual(animate: false);
            };
        }

        // ---------------- window composition (acrylic + rounded + dark) ----------------

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int on = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));

            int round = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            // Prefer acrylic; fall back to mica. Only make the WPF surface transparent if one took,
            // otherwise we'd see straight through to the desktop.
            int backdrop = DWMSBT_ACRYLIC;
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            if (hr != 0)
            {
                backdrop = DWMSBT_MICA;
                hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }

            _acrylicOn = hr == 0;
            if (_acrylicOn)
            {
                HwndSource src = HwndSource.FromHwnd(hwnd);
                if (src?.CompositionTarget != null)
                    src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
            else
            {
                // no system backdrop available — use a solid glass surface instead of a see-through one
                RootShell.Background = (Brush)FindResource("P.ScrimSolid");
            }
        }

        // ---------------- caption buttons ----------------

        private void btnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void btnMax_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            btnMaxGlyph.Data = System.Windows.Media.Geometry.Parse(WindowState == WindowState.Maximized ? "M0,3 H7 V10 H0 Z M3,0 H10 V7" : "M0.5,0.5 H9.5 V9.5 H0.5 Z");
            // keep the rounded shell clear of the screen edges when maximized
            RootShell.Margin = WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ---------------- footer / card actions ----------------

        private void btnSkipTweaks_Click(object sender, RoutedEventArgs e) => _vm.SkipTweaks();

        private void btnPrimary_Click(object sender, RoutedEventArgs e)
        {
            // in the prototype, "Apply my changes now" just advances the simulated sequence
            if (_vm.State == MonitorPrototypeViewModel.DemoState.Warning ||
                _vm.State == MonitorPrototypeViewModel.DemoState.Error)
            {
                _vm.ShowState(MonitorPrototypeViewModel.DemoState.ApplyingServiceChanges);
            }
            else if (_vm.IsFinished)
            {
                Close();
            }
        }

        private void btnFinish_Click(object sender, RoutedEventArgs e) => Close();

        // ---------------- prototype control strip ----------------

        private async void sim_Click(object sender, RoutedEventArgs e) => await _vm.PlaySimulationAsync();

        private void reset_Click(object sender, RoutedEventArgs e) => _vm.Reset();

        private void state_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string tag &&
                Enum.TryParse(tag, out MonitorPrototypeViewModel.DemoState state))
            {
                _vm.ShowState(state);
            }
        }

        // ---------------- vm -> visuals ----------------

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MonitorPrototypeViewModel.PhaseLabel):
                    PlayHeroTransition();
                    break;
                case nameof(MonitorPrototypeViewModel.Percent):
                case nameof(MonitorPrototypeViewModel.Indeterminate):
                    UpdateProgressVisual(animate: true);
                    break;
                case nameof(MonitorPrototypeViewModel.Status):
                    ApplyStatus(_vm.Status);
                    break;
            }
        }

        private void ProgressHost_SizeChanged(object sender, SizeChangedEventArgs e) =>
            UpdateProgressVisual(animate: false);

        private void UpdateProgressVisual(bool animate)
        {
            double host = ProgressHost.ActualWidth;
            if (host <= 0)
                return;

            if (_vm.Indeterminate)
            {
                ProgressFill.BeginAnimation(WidthProperty, null);
                ProgressFill.Visibility = Visibility.Collapsed;
                Shimmer.Visibility = Visibility.Visible;
                StartShimmer(host);
                return;
            }

            StopShimmer();
            Shimmer.Visibility = Visibility.Collapsed;
            ProgressFill.Visibility = Visibility.Visible;

            double target = host * Math.Max(0, Math.Min(100, _vm.Percent)) / 100.0;
            if (animate)
            {
                var a = new DoubleAnimation(target, TimeSpan.FromMilliseconds(320))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };
                ProgressFill.BeginAnimation(WidthProperty, a);
            }
            else
            {
                ProgressFill.BeginAnimation(WidthProperty, null);
                ProgressFill.Width = target;
            }
        }

        private void StartShimmer(double host)
        {
            _shimmer?.Stop(this);
            double w = Shimmer.Width;
            var move = new DoubleAnimation(-w, host, TimeSpan.FromSeconds(1.25))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            Storyboard.SetTarget(move, ShimmerShift);
            Storyboard.SetTargetProperty(move, new PropertyPath(TranslateTransform.XProperty));
            _shimmer = new Storyboard();
            _shimmer.Children.Add(move);
            _shimmer.Begin(this, true);
        }

        private void StopShimmer()
        {
            _shimmer?.Stop(this);
            _shimmer = null;
        }

        private void ApplyStatus(MonitorPrototypeViewModel.StatusKind kind)
        {
            string key = kind switch
            {
                MonitorPrototypeViewModel.StatusKind.Active => "P.Active",
                MonitorPrototypeViewModel.StatusKind.Success => "P.Success",
                MonitorPrototypeViewModel.StatusKind.Caution => "P.Caution",
                MonitorPrototypeViewModel.StatusKind.Danger => "P.Danger",
                _ => "P.Neutral",
            };
            var brush = (Brush)FindResource(key);
            StatusDot.Fill = brush;
            StatusGlow.Fill = brush;

            _pulse?.Stop(this);
            _pulse = null;

            if (kind == MonitorPrototypeViewModel.StatusKind.Active)
            {
                var a = new DoubleAnimation(0.15, 0.55, TimeSpan.FromSeconds(0.9))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                };
                Storyboard.SetTarget(a, StatusGlow);
                Storyboard.SetTargetProperty(a, new PropertyPath(OpacityProperty));
                _pulse = new Storyboard();
                _pulse.Children.Add(a);
                _pulse.Begin(this, true);
            }
            else
            {
                StatusGlow.BeginAnimation(OpacityProperty, null);
                StatusGlow.Opacity = 0.32;
            }
        }

        private void PlayHeroTransition()
        {
            Hero.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(180)));
            _heroShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.PropertyChanged -= OnVmChanged;
            StopShimmer();
            _pulse?.Stop(this);
            base.OnClosed(e);
        }

        // ---------------- DWM interop ----------------

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMWCP_ROUND = 2;
        private const int DWMSBT_MICA = 2;
        private const int DWMSBT_ACRYLIC = 3;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}
