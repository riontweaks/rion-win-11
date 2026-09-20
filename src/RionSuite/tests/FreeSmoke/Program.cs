using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RionHub;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;

internal static class Program
{
    [STAThread] static int Main()
    {
        try {
            void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
            Check(EditionPolicy.IsFree,"Free policy");
            Check(TweakBundleCatalog.All.All(b=>b.Section=="MMCSS"||b.Id is "bundle-game-capture" or "bundle-priority-separation"),"Free-only tweak definitions");
            Check(!typeof(ShellViewModel).Assembly.GetTypes().Any(t=>t.Name.StartsWith("Bios")||t.Name=="CustomerGate"),"No paid firmware/customer implementation");
            Check(!typeof(ShellViewModel).Assembly.GetReferencedAssemblies().Any(a=>a.Name is "ALDX.App" or "NvidiaControlPanel" or "PowerSettingsExplorer.Wpf"),"No advanced module dependencies");
            var app=new Application();app.Resources.MergedDictionaries.Add(new(){Source=new Uri("/Rion Win 11;component/Theme/Blue.xaml",UriKind.Relative)});
            var vm=new ShellViewModel();
            Check(!vm.NavigateToKey("BIOS")&&!vm.NavigateToKey("Custom Power Plan"),"Paid navigation unavailable");
            Check(vm.Nav.SelectMany(n=>n.Children).Any(n=>n.Title=="AMD driver installation")&&vm.Nav.SelectMany(n=>n.Children).Any(n=>n.Title=="NVIDIA driver installation"),"Both driver workflows reachable before vendor driver detection");
            var icon=new BitmapImage(new Uri("pack://application:,,,/Rion Win 11;component/Assets/InstallerIcons/DisplayDriverUninstallerDDU.png"));Check(icon.PixelWidth>0,"DDU icon embedded");
            Directory.CreateDirectory("artifacts/free-smoke");
            foreach(int width in new[]{560,900,1280,1600})foreach(int dpi in new[]{96,144,192}){
                var page=(FrameworkElement)vm.ActiveContent!;var host=new Border{Background=(Brush)app.FindResource("Bg0"),Child=page};host.Measure(new Size(width,760));host.Arrange(new Rect(0,0,width,760));host.UpdateLayout();
                var bmp=new RenderTargetBitmap(width*dpi/96,760*dpi/96,dpi,dpi,PixelFormats.Pbgra32);bmp.Render(host);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create($"artifacts/free-smoke/home-{width}-{dpi}.png"))png.Save(file);host.Child=null;
            }
            var shell=new ShellWindow();shell.ApplyTemplate();shell.Close();
            Console.WriteLine("PASS: Free startup surface, source boundaries, catalog, both driver routes, DDU icon and 12 layout renders. No system settings changed.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
