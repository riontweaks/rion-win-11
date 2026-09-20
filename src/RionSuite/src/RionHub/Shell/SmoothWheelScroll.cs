using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace RionHub.Shell;

/// <summary>Pixel-viewer wheel easing only. Logical lists retain their IScrollInfo line commands.</summary>
public static class SmoothWheelScroll
{
    private sealed class State
    {
        public bool Running;
        public double Target;
        public int Generation;
    }
    private static readonly ConditionalWeakTable<ScrollViewer, State> States = new();
    private static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached(
        "Offset", typeof(double), typeof(SmoothWheelScroll), new PropertyMetadata(0d, OnOffset));

    public static double PendingOffset(ScrollViewer viewer) => States.TryGetValue(viewer, out var state) && state.Running
        ? Math.Clamp(state.Target, 0, viewer.ScrollableHeight) : viewer.VerticalOffset;

    public static bool TryMove(ScrollViewer viewer, long notches, int lines)
    {
        if (viewer.CanContentScroll || !AppMotion.CanAnimate(viewer)) { Cancel(viewer); return false; }
        if (notches == 0) return true;
        var state = States.GetValue(viewer, v =>
        {
            v.Unloaded += OnUnloaded;
            v.IsVisibleChanged += OnVisibility;
            v.PreviewMouseDown += OnMouseDown;
            v.PreviewKeyDown += OnKeyDown;
            v.PreviewTouchDown += OnTouchDown;
            v.PreviewStylusDown += OnStylusDown;
            v.RequestBringIntoView += OnBringIntoView;
            return new State();
        });
        double from = viewer.VerticalOffset;
        double step = lines < 0 ? viewer.ViewportHeight : lines * 16d;
        double target = Math.Clamp(PendingOffset(viewer) - notches * step, 0, viewer.ScrollableHeight);
        Cancel(viewer);
        state.Target = target;
        state.Running = true;
        int generation = state.Generation;
        viewer.SetValue(OffsetProperty, from);
        var animation = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(140))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) =>
        {
            if (state.Generation != generation) return;
            state.Running = false;
            viewer.BeginAnimation(OffsetProperty, null);
            viewer.SetValue(OffsetProperty, target);
            viewer.ScrollToVerticalOffset(Math.Clamp(target, 0, viewer.ScrollableHeight));
        };
        viewer.BeginAnimation(OffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        return true;
    }

    private static void OnOffset(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var viewer = (ScrollViewer)target;
        if (!States.TryGetValue(viewer, out var state) || !state.Running) return;
        if (!AppMotion.CanAnimate(viewer))
        {
            double destination = state.Target;
            Cancel(viewer);
            viewer.ScrollToVerticalOffset(Math.Clamp(destination, 0, viewer.ScrollableHeight));
            return;
        }
        viewer.ScrollToVerticalOffset(Math.Clamp((double)args.NewValue, 0, viewer.ScrollableHeight));
    }
    public static void Cancel(ScrollViewer viewer)
    {
        if (!States.TryGetValue(viewer, out var state)) return;
        state.Running = false;
        state.Generation++;
        viewer.BeginAnimation(OffsetProperty, null);
    }
    private static void OnUnloaded(object sender, RoutedEventArgs args) => Cancel((ScrollViewer)sender);
    private static void OnVisibility(object sender, DependencyPropertyChangedEventArgs args) { if (!(bool)args.NewValue) Cancel((ScrollViewer)sender); }
    private static void OnMouseDown(object sender, MouseButtonEventArgs args) => Cancel((ScrollViewer)sender);
    private static void OnKeyDown(object sender, KeyEventArgs args) => Cancel((ScrollViewer)sender);
    private static void OnTouchDown(object? sender, TouchEventArgs args) { if (sender is ScrollViewer viewer) Cancel(viewer); }
    private static void OnStylusDown(object sender, StylusDownEventArgs args) => Cancel((ScrollViewer)sender);
    private static void OnBringIntoView(object sender, RequestBringIntoViewEventArgs args) => Cancel((ScrollViewer)sender);
}
