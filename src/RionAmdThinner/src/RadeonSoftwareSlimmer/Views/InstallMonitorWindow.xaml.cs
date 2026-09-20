using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using RadeonSoftwareSlimmer.ViewModels.Wizard;

namespace RadeonSoftwareSlimmer.Views
{
    public partial class InstallMonitorWindow : Window
    {
        private readonly Action _onFinish;
        private readonly InstallMonitor _monitor;

        public InstallMonitorWindow(InstallMonitor monitor, Action onFinish)
        {
            InitializeComponent();
            _monitor = monitor;
            _onFinish = onFinish;
            DataContext = monitor;
            monitor.PropertyChanged += OnMonitorChanged;
            Loaded += (s, e) => ApplyStatus(monitor.Status);
            Closed += (s, e) => monitor.PropertyChanged -= OnMonitorChanged;
        }

        // ---------------- glass window composition ----------------

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

            int backdrop = DWMSBT_ACRYLIC;
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            if (hr != 0)
            {
                backdrop = DWMSBT_MICA;
                hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }

            HwndSource src = HwndSource.FromHwnd(hwnd);
            if (hr == 0)
            {
                if (src?.CompositionTarget != null)
                    src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
            else
            {
                RootShell.Background = (Brush)FindResource("ScrimSolid");
                if (src?.CompositionTarget != null)
                    src.CompositionTarget.BackgroundColor = Colors.Black;
            }
        }

        // ---------------- vm -> status dot ----------------

        private void OnMonitorChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InstallMonitor.Status))
                ApplyStatus(_monitor.Status);
        }

        private void ApplyStatus(MonitorStatus status)
        {
            string key = status switch
            {
                MonitorStatus.Active => "InfoCol",
                MonitorStatus.Success => "GoodCol",
                MonitorStatus.Caution => "CautionCol",
                MonitorStatus.Danger => "DirtyCol",
                _ => "TextTertiary",
            };
            var brush = (Brush)FindResource(key);
            StatusDot.Fill = brush;
            StatusGlow.Fill = brush;
        }

        // ---------------- buttons ----------------

        private void btnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void btnFinish_Click(object sender, RoutedEventArgs e)
        {
            _onFinish?.Invoke();
            Close();
        }

        private void btnSkipTweaks_Click(object sender, RoutedEventArgs e) => _monitor.CancelGpuTweaks();

        private async void btnManualApply_Click(object sender, RoutedEventArgs e) => await _monitor.RunManualApplyAsync();

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
