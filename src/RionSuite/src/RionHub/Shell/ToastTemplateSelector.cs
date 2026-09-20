using System.Windows;
using System.Windows.Controls;

namespace RionHub.Shell;

/// <summary>Picks the plain vs. progress toast visual for <c>ShellWindow.xaml</c>'s toast
/// overlay, since <see cref="ShellViewModel.Toasts"/> holds both <see cref="ToastVm"/> and
/// <see cref="ProgressToastVm"/> entries side by side.</summary>
public sealed class ToastTemplateSelector : DataTemplateSelector
{
    public DataTemplate? PlainTemplate { get; set; }
    public DataTemplate? ProgressTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is ProgressToastVm ? ProgressTemplate : PlainTemplate;
}
