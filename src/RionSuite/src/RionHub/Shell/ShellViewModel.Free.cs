using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using RionHub.Shell;
using RionHub.Modules.Tools.General;
using RionHub.Modules.Tools.Windows;
using RionHub.Modules.About;

namespace RionHub;

public sealed partial class ShellViewModel
{
    private ObservableCollection<NavItem> BuildFreeNav()
    {
        var home = Leaf("Dashboard", "", CreateFreeHome);
        var tweaks = Leaf("Tweaks", char.ConvertFromUtf32(59663), () => new GeneralTweaksView { DataContext = Tools.General });
        var windows = Leaf("Windows", char.ConvertFromUtf32(59136), () => new WindowsToolsView { DataContext = Tools.Windows });
        var gpu = new NavItem("GPU Manager") { IconGeometry = GpuIcon() };
        gpu.Children.Add(Leaf("AMD driver installation", "", () => Amd.DriverContent));
        gpu.Children.Add(Leaf("NVIDIA driver installation", "", () => NvidiaDriverContent));
        var drivers = Leaf("Drivers", "", () => new Features.DriversPage());
        var utilities = Leaf("Utilities", char.ConvertFromUtf32(59192), () => new Features.UtilitiesPage());
        var about = Leaf("About", char.ConvertFromUtf32(59718), () => new AboutView());
        // No hidden paid leaves are constructed; title/deep-link lookup uses this same tree.
        home.IconGeometry = System.Windows.Media.Geometry.Parse("M2,8 L9,2 L16,8 V16 H11 V11 H7 V16 H2 Z");
        tweaks.IconGeometry = System.Windows.Media.Geometry.Parse("M11,2 A5,5 0 0 0 7,9 L2,14 A1.5,1.5 0 0 0 4,16 L9,11 A5,5 0 0 0 16,7 L13,9 L10,6 Z");
        windows.IconGeometry = System.Windows.Media.Geometry.Parse("M2,2 H7 V7 H2 Z M11,2 H16 V7 H11 Z M2,11 H7 V16 H2 Z M11,11 H16 V16 H11 Z");
        drivers.IconGeometry = System.Windows.Media.Geometry.Parse("M2,3 H16 V13 H2 Z M5,16 H13 M9,13 V16 M6,6 H12 V10 H6 Z");
        utilities.IconGeometry = System.Windows.Media.Geometry.Parse("M2,6 H16 V16 H2 Z M6,6 V3 H12 V6 M2,10 H16 M7,10 V12 H11 V10");
        about.IconGeometry = System.Windows.Media.Geometry.Parse("M9,2 A7,7 0 1 1 9,16 A7,7 0 1 1 9,2 M9,8 V13 M9,5 V5.5");
        return new() { home, tweaks, windows, gpu, drivers, utilities, about };
    }

    private UserControl CreateFreeHome()
    {
        var panel = new StackPanel { Margin = new Thickness(24,18,24,18) };
        var title = new TextBlock { Text = "Rion Win 11 · Free version" };
        title.SetResourceReference(FrameworkElement.StyleProperty,"H1"); panel.Children.Add(title);
        panel.Children.Add(new TextBlock { Text = "Driver installation, utilities and core Windows tweaks.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,16) });
        foreach (var label in new[] { "Tweaks", "AMD driver installation", "NVIDIA driver installation", "Utilities" })
        {
            var button = new Button { Content = label, HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0,0,0,6), Padding = new Thickness(12,9,12,9) };
            button.Click += (_,_) => NavigateToKey(label); panel.Children.Add(button);
        }
        return new UserControl { Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }
}
