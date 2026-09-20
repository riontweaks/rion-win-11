using System.Windows;

namespace RionHub.Modules.Tools;

/// <summary>Shared responsive layout for native utility pages and their row templates.</summary>
public static class ToolLayout
{
    public static readonly DependencyProperty TrackSizeProperty = DependencyProperty.RegisterAttached(
        "TrackSize", typeof(bool), typeof(ToolLayout), new PropertyMetadata(false, TrackChanged));
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.RegisterAttached(
        "IsCompact", typeof(bool), typeof(ToolLayout), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetTrackSize(DependencyObject value) => (bool)value.GetValue(TrackSizeProperty);
    public static void SetTrackSize(DependencyObject value, bool track) => value.SetValue(TrackSizeProperty, track);
    public static bool GetIsCompact(DependencyObject value) => (bool)value.GetValue(IsCompactProperty);
    public static void SetIsCompact(DependencyObject value, bool compact) => value.SetValue(IsCompactProperty, compact);
    private static void TrackChanged(DependencyObject value, DependencyPropertyChangedEventArgs e)
    {
        if (value is not FrameworkElement element) return;
        element.SizeChanged -= SizeChanged;
        if ((bool)e.NewValue) { element.SizeChanged += SizeChanged; SetIsCompact(element, element.ActualWidth < 660); }
    }
    private static void SizeChanged(object sender, SizeChangedEventArgs e) => SetIsCompact((DependencyObject)sender, e.NewSize.Width < 660);
}
