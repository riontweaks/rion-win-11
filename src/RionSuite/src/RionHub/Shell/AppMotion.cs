using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RionHub.Shell;

public enum MotionMode { System, On, Off }

/// <summary>Short, event-driven motion. No idle timers, layout animation or deferred interaction.</summary>
public static class AppMotion
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(AppMotion), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);

    public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
        "Mode", typeof(MotionMode), typeof(AppMotion), new FrameworkPropertyMetadata(MotionMode.System, FrameworkPropertyMetadataOptions.Inherits));
    public static MotionMode GetMode(DependencyObject target) => (MotionMode)target.GetValue(ModeProperty);
    public static void SetMode(DependencyObject target, MotionMode value) => target.SetValue(ModeProperty, value);

    public static bool CanAnimate(FrameworkElement target) => GetIsEnabled(target) &&
        GetMode(target) != MotionMode.Off && (GetMode(target) == MotionMode.On || SystemParameters.ClientAreaAnimation) && !SystemParameters.HighContrast &&
        target.IsVisible && PresentationSource.FromVisual(target) != null;

    public static readonly DependencyProperty HeadingEntranceProperty = DependencyProperty.RegisterAttached(
        "HeadingEntrance", typeof(bool), typeof(AppMotion), new PropertyMetadata(false, OnHeadingEntranceChanged));
    public static bool GetHeadingEntrance(DependencyObject target) => (bool)target.GetValue(HeadingEntranceProperty);
    public static void SetHeadingEntrance(DependencyObject target, bool value) => target.SetValue(HeadingEntranceProperty, value);

    private static void OnHeadingEntranceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBlock text) return;
        text.Loaded -= HeadingLoaded;
        text.Unloaded -= HeadingUnloaded;
        text.TargetUpdated -= HeadingTargetUpdated;
        if ((bool)args.NewValue)
        {
            text.Loaded += HeadingLoaded;
            text.Unloaded += HeadingUnloaded;
            text.TargetUpdated += HeadingTargetUpdated;
        }
        else text.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private static void HeadingLoaded(object sender, RoutedEventArgs args) => FadeHeading((TextBlock)sender);
    private static void HeadingTargetUpdated(object? sender, System.Windows.Data.DataTransferEventArgs args)
    {
        if (args.Property == TextBlock.TextProperty && sender is TextBlock text) FadeHeading(text);
    }
    private static void HeadingUnloaded(object sender, RoutedEventArgs args) => ((TextBlock)sender).BeginAnimation(UIElement.OpacityProperty, null);
    private static void FadeHeading(TextBlock text)
    {
        text.BeginAnimation(UIElement.OpacityProperty, null);
        if (!CanAnimate(text)) return;
        double opacity = text.Opacity;
        var animation = new DoubleAnimation(opacity * .72, opacity, TimeSpan.FromMilliseconds(160))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) => text.BeginAnimation(UIElement.OpacityProperty, null);
        text.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}

/// <summary>Animates the shell-owned host, never replaces a hosted module's transforms.</summary>
public sealed class PageTransitionHost : ContentControl
{
    private readonly TranslateTransform entrance = new();
    public PageTransitionHost()
    {
        RenderTransform = entrance;
        Loaded += (_, _) => Enter();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => { if (!IsVisible) Stop(); };
    }
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        Enter();
    }
    private void Stop()
    {
        BeginAnimation(OpacityProperty, null);
        entrance.BeginAnimation(TranslateTransform.YProperty, null);
    }
    private void Enter()
    {
        Stop();
        if (Content == null || !AppMotion.CanAnimate(this)) return;
        var duration = TimeSpan.FromMilliseconds(180);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(.88, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
        fade.Completed += (_, _) => Stop();
        BeginAnimation(OpacityProperty, fade);
        entrance.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
    }
}
