using System.Windows;
using System.Windows.Controls;

namespace RionHub.Modules.Tools.Windows;

public partial class WindowsToolsView : UserControl
{
    public WindowsToolsView()
    {
        InitializeComponent();
    }

    private void AppsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowsToolsViewModel vm) vm.Section = InstallerSection.Apps;
    }

    private void WindowsAppsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowsToolsViewModel vm) vm.Section = InstallerSection.WindowsApps;
    }

    private void RuntimesToggle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowsToolsViewModel vm) vm.Section = InstallerSection.Runtimes;
    }

    private void UtilitiesToggle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowsToolsViewModel vm) vm.Section = InstallerSection.Utilities;
    }
}
