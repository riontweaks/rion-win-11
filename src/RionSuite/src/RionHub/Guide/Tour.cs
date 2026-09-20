using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace RionHub.Guide;

/// <summary>
/// The guided tour finds its target controls by <c>AutomationProperties.AutomationId</c> — a
/// built-in WPF attached property, so target views in the module assemblies can be tagged
/// (<c>AutomationProperties.AutomationId="select-driver-existing"</c>) without referencing this
/// assembly.
/// </summary>
public static class Tour
{
    /// <summary>Set by ShellWindow so the Tutorial page's "start" button can kick off a tour.</summary>
    public static Action<string> Launch { get; set; }

    /// <summary>Depth-first search of the visual tree for the visible element whose AutomationId
    /// is <paramref name="id"/>.</summary>
    public static FrameworkElement FindById(DependencyObject root, string id)
    {
        if (root == null || string.IsNullOrEmpty(id)) return null;

        if (root is FrameworkElement fe && fe.IsVisible &&
            string.Equals(AutomationProperties.GetAutomationId(fe), id, StringComparison.Ordinal))
            return fe;

        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            FrameworkElement hit = FindById(VisualTreeHelper.GetChild(root, i), id);
            if (hit != null) return hit;
        }
        return null;
    }
}
