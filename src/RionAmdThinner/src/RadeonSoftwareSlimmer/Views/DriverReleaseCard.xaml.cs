using System;
using System.Windows.Controls;
using System.Windows.Threading;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Views
{
    public partial class DriverReleaseCard : UserControl
    {
        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(15) };
        public DriverReleaseCard()
        {
            InitializeComponent();
            timer.Tick += async (_, _) => { if (IsVisible && DataContext is DriverReleaseCardViewModel vm) await vm.RefreshAsync(); };
            Loaded += (_, _) => UpdateActivity();
            Unloaded += (_, _) => timer.Stop();
            IsVisibleChanged += (_, _) => UpdateActivity();
            DataContextChanged += (_, _) => UpdateActivity();
        }
        private async void UpdateActivity()
        {
            timer.Stop();
            if (!IsLoaded || !IsVisible || DataContext is not DriverReleaseCardViewModel vm) return;
            timer.Start();
            await vm.RefreshAsync();
        }
    }
}
