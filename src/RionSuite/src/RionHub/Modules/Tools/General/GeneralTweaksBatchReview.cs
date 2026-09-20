using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlassKit;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RionHub.Shell;

namespace RionHub.Modules.Tools.General;

public sealed partial class GeneralTweaksViewModel
{
    private async Task ApplyEntireCatalogAsync()
    {
        if (!CanStartBatch) return;
        var plan = CatalogBatchPlan.Create(_all.Select(r => r.Bundle));
        var panel = new DockPanel { Margin = new Thickness(18) };
        var heading = new TextBlock { Text = "Review all tweaks", FontSize = 22, FontWeight = FontWeights.Bold };
        heading.SetResourceReference(TextBlock.FontFamilyProperty, "DisplayFont");
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0,0,8,0) };
        var apply = new Button { Content = "Apply reviewed tweaks" };
        actions.Children.Add(cancel); actions.Children.Add(apply); DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
        var contents = new StackPanel();
        contents.Children.Add(new TextBlock { Text = "All sections are included. Individual-choice settings stay unchanged. Check experimental or security-sensitive settings only when you want to test them. Some changes require a restart.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12) });
        foreach (var entry in plan)
        {
            var label = new TextBlock { Text = entry.Tweak.Title + " → " + entry.Tweak.TargetLabel +
                (entry.RequiresOptIn ? " · Experimental / review required" : "") +
                (entry.Exclusion.Length > 0 ? "\n" + entry.Exclusion : "") +
                (string.IsNullOrWhiteSpace(entry.Tweak.TradeOff) ? "" : "\n" + entry.Tweak.TradeOff), TextWrapping = TextWrapping.Wrap };
            var check = new CheckBox { Content = label, IsChecked = entry.Included, IsEnabled = entry.Available, Margin = new Thickness(0,0,0,12), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            check.Checked += (_, _) => entry.Included = true;
            check.Unchecked += (_, _) => entry.Included = false;
            contents.Children.Add(check);
        }
        panel.Children.Add(new ScrollViewer { Content = contents, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var dialog = new GlassWindow { Title = "Review Apply all", Content = panel, Width = Math.Min(720, SystemParameters.WorkArea.Width), Height = Math.Min(740, SystemParameters.WorkArea.Height), Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)Application.Current.FindResource("Bg1"), Foreground = (Brush)Application.Current.FindResource("Text"), FontFamily = (FontFamily)Application.Current.FindResource("AppFont") };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        apply.Click += (_, _) => dialog.DialogResult = true;
        if (dialog.ShowDialog() != true || !CanStartBatch || !TweakConsent.Request("Apply reviewed tweaks")) return;
        using (TweakConsent.ApprovedBatch())
        {
            SetBatchBusy(true); Results.Clear();
            var toast = ToastCenter.ShowProgress("Applying reviewed tweaks…");
            void Report(TweakOperationResult result) { Results.Add(result); Raise(nameof(HasResults)); Raise(nameof(ResultSummary)); toast.Report(Results.Count, plan.Count + (EditionPolicy.HasAdvancedTools ? 2 : 0), result.Display); }
            try
            {
                await CatalogBatchPlan.RunAsync(plan, tweaks => Task.Run(() => new TweakService(new WindowsRegistry()).RunGroup(tweaks, true)), Report);
                if (Results.Any(r => r.Status == TweakOperationStatus.Failed)) toast.Fail(ResultSummary); else toast.Complete(ResultSummary);
            }
            catch (Exception ex) { toast.Fail("Batch interrupted: " + ex.Message); }
            finally { SetBatchBusy(false); foreach (var row in _all) row.Refresh(); Raise(nameof(AppliedSummary)); }
        }
    }

    private void SetBatchBusy(bool busy)
    {
        _isBulkBusy = busy;
        foreach (var row in _all) row.IsLocked = busy;
        Raise(nameof(CanEditTweaks)); ApplyAllCommand.RaiseCanExecuteChanged(); ApplyAllTweaksCommand.RaiseCanExecuteChanged(); RevertAllCommand.RaiseCanExecuteChanged();
    }
}
