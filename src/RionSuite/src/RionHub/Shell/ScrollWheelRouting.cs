using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RionHub.Shell;

/// <summary>One wheel policy for hosted pages, nested lists and dialog scroll viewers.</summary>
public static class ScrollWheelRouting
{
    private static bool registered;
    private sealed class WheelRemainder { public long Delta; }
    private static readonly ConditionalWeakTable<ScrollViewer, WheelRemainder> Remainders = new();

    public static void Register()
    {
        if (registered) return;
        registered = true;
        EventManager.RegisterClassHandler(typeof(ScrollViewer), Mouse.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Empty space inside the viewport must participate in hit testing too.
        if (sender is ScrollViewer { Background: null } viewer)
            viewer.SetCurrentValue(Control.BackgroundProperty, Brushes.Transparent);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || SystemParameters.WheelScrollLines == 0
            || Keyboard.Modifiers != ModifierKeys.None) return;

        var ancestors = new List<DependencyObject>();
        for (var node = e.OriginalSource as DependencyObject; node != null; node = Parent(node))
            ancestors.Add(node);

        // Popup lists keep their own selection and wheel behavior. Do not scroll the page
        // behind an open dropdown (including a ComboBox's non-popup portion).
        if (ancestors.OfType<ComboBox>().Any(c => c.IsDropDownOpen)) return;

        var viewers = ancestors.OfType<ScrollViewer>().Where(v => v.IsEnabled
            && v.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled).ToList();
        var target = viewers.FirstOrDefault(v => CanMove(v, e.Delta));
        if (target != null)
        {
            Move(target, e.Delta);
            e.Handled = true;
        }
        else if (viewers.Count > 0)
        {
            // At the page boundary, a wheel gesture must not change a closed dropdown
            // or a tuning control instead. No event is re-raised, avoiding routing loops.
            e.Handled = true;
        }
    }

    private static bool CanMove(ScrollViewer viewer, int delta) => viewer.ScrollableHeight > 0
        && (delta > 0 ? SmoothWheelScroll.PendingOffset(viewer) > 0 : SmoothWheelScroll.PendingOffset(viewer) < viewer.ScrollableHeight);

    private static void Move(ScrollViewer viewer, int delta)
    {
        var remainder = Remainders.GetOrCreateValue(viewer);
        remainder.Delta += delta;
        long notches = remainder.Delta / Mouse.MouseWheelDeltaForOneLine;
        remainder.Delta %= Mouse.MouseWheelDeltaForOneLine;
        int lines = SystemParameters.WheelScrollLines;
        if (SmoothWheelScroll.TryMove(viewer, notches, lines)) return;
        // Line/Page commands delegate to IScrollInfo, preserving pixel versus logical
        // item units. Fractional wheel events accumulate instead of jumping whole rows.
        for (long n = 0; n < Math.Abs(notches); n++)
        {
            if (lines < 0)
            {
                if (notches > 0) viewer.PageUp(); else viewer.PageDown();
            }
            else for (int line = 0; line < lines; line++)
            {
                if (notches > 0) viewer.LineUp(); else viewer.LineDown();
            }
        }
    }

    private static DependencyObject? Parent(DependencyObject node)
    {
        if (node is Visual or Visual3D)
            return VisualTreeHelper.GetParent(node) ?? (node as FrameworkElement)?.Parent;
        if (node is FrameworkContentElement content)
            return content.Parent ?? ContentOperations.GetParent(content);
        return LogicalTreeHelper.GetParent(node);
    }
}
