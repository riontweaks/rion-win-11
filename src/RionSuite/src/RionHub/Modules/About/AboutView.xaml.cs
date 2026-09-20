using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using RionHub.Shell;

namespace RionHub.Modules.About;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/Rion Win 11;component/Assets/rion.ico"),
            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        AppLogo.Source = decoder.Frames.OrderByDescending(frame => frame.PixelWidth).First();
        DataContext = new AboutViewModel();
    }

    private void Discord_Click(object sender, RoutedEventArgs e) =>
        Open("https://discord.gg/5vtgShuks3");

    private void Licenses_Click(object sender,RoutedEventArgs e)
    {
        var assembly=typeof(AboutView).Assembly;
        var text=new System.Text.StringBuilder();
        foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("Rion.Licenses.")).Order())
        {using var stream=assembly.GetManifestResourceStream(name)!;using var reader=new StreamReader(stream);text.AppendLine(name[14..]).AppendLine().AppendLine(reader.ReadToEnd()).AppendLine();}
        var box=new TextBox{Text=text.ToString(),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(16)};
        var window=new Window{Title="Licenses and notices",Owner=Window.GetWindow(this),Width=800,Height=600,Content=box,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        window.SetResourceReference(BackgroundProperty,"Bg1");window.SetResourceReference(ForegroundProperty,"Text");window.SetResourceReference(FontFamilyProperty,"AppFont");window.ShowDialog();
    }
    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { ToastCenter.Show("Couldn't open that link.", isError: true); }
    }
}
