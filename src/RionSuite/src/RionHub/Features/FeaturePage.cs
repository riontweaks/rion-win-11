using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace RionHub.Features;

public class FeaturePage : UserControl
{
    protected readonly DockPanel Layout = new() { Margin = new Thickness(28, 20, 28, 20) };
    protected readonly WrapPanel Actions = new() { Margin = new Thickness(0, 12, 0, 12) };
    protected readonly TextBlock Status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
    protected readonly StackPanel Header = new();
    protected bool Busy;
    public FeaturePage(string title, string description)
    {
        SetResourceReference(FontFamilyProperty, "AppFont");
        UseLayoutRounding = true;
        Content = Layout;
        var heading = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap };
        heading.SetResourceReference(StyleProperty, "H1");
        Header.Children.Add(heading);
        Header.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,0) });
        Header.Children.Add(Actions);
        var headerScroll = new ScrollViewer { Content=Header, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        SizeChanged += (_,_) => headerScroll.MaxHeight = Math.Max(180,ActualHeight * .58);
        DockPanel.SetDock(headerScroll, Dock.Top); Layout.Children.Add(headerScroll);
        DockPanel.SetDock(Status, Dock.Bottom); Layout.Children.Add(Status);
    }
    protected Button Action(string label, Func<Task> work)
    {
        var button = new Button { Content = label, Margin = new Thickness(0,0,8,8), Padding = new Thickness(12,6,12,6) };
        button.Click += async (_, _) =>
        {
            if (Busy) return;
            Busy = true; button.IsEnabled = false;
            try { await work(); } catch (Exception e) { Status.Text = e.Message; }
            finally { Busy = false; button.IsEnabled = true; }
        };
        Actions.Children.Add(button); return button;
    }
    protected Button Action(string label, Action work) => Action(label, () => { work(); return Task.CompletedTask; });
    protected static DataGrid Table(params (string Label, string Path)[] columns)
    {
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single, EnableRowVirtualization = true, EnableColumnVirtualization = true,
            Background = Brushes.Transparent, RowBackground = new SolidColorBrush(Color.FromRgb(30,36,46)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(24,29,37)), Foreground = Brushes.White,
            GridLinesVisibility = DataGridGridLinesVisibility.None, HeadersVisibility = DataGridHeadersVisibility.Column };
        foreach (var (label,path) in columns) grid.Columns.Add(new DataGridTextColumn { Header=label, Binding=new Binding(path), Width=new DataGridLength(1,DataGridLengthUnitType.Star) });
        return grid;
    }
}
