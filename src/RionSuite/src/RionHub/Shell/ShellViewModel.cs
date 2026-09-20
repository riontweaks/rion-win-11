using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using RionHub.Modules.MergedManager;
using RionHub.Modules.Tools;
using RionHub.Shell;
using NvidiaDriverTool.ViewModels;
using NvidiaDriverTool.Views;

namespace RionHub;
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly List<NavItem> _leaves=[];
    private NavItem? _selectedTab;
    private object? _activeContent;
    private string _headerTitle="Dashboard";
    private MergedManagerViewModel? amd;
    private ToolsViewModel? tools;
    private NvidiaWizardView? nvidia;
    public MergedManagerViewModel Amd { get { if(amd==null){amd=new();amd.MonitorWindowRequested+=()=>MonitorWindowRequested?.Invoke();}return amd; } }
    public ToolsViewModel Tools => tools??=new();
    private UserControl NvidiaDriverContent=>nvidia??=new(){DataContext=new NvidiaWizardViewModel()};
    public ObservableCollection<NavItem> Nav {get;}
    public ObservableCollection<NavRow> SubNav {get;}=[];
    public ObservableCollection<object> Toasts {get;}=[];
    public string EditionTitle=>"Rion Win 11 · Free version";
    public string SectionTitle=>SelectedTab?.Title??"";
    public bool HasSubNav=>SubNav.Count>0;
    public NavItem? SelectedTab {get=>_selectedTab;private set=>Set(ref _selectedTab,value);}
    public object? ActiveContent {get=>_activeContent;private set=>Set(ref _activeContent,value);}
    public string HeaderTitle {get=>_headerTitle;private set=>Set(ref _headerTitle,value);}
    public event Action? MonitorWindowRequested;
    private Modules.Tools.Windows.UtilityToolRowViewModel? ddu;
    public Modules.Tools.Windows.UtilityToolRowViewModel Ddu=>ddu??=new(RadeonSoftwareSlimmer.Optimize.UtilityToolCatalog.All.Single(e=>e.Source=="Wagnardsoft.DisplayDriverUninstaller"));
    public bool ShowDdu=>HeaderTitle is "AMD driver installation" or "NVIDIA driver installation";
    public ShellViewModel(){Nav=BuildFreeNav();SelectLeaf(_leaves[0]);ToastCenter.Requested+=async(message,error)=>{var toast=new ToastVm{Message=message,IsError=error};Toasts.Add(toast);await Task.Delay(2200);Toasts.Remove(toast);};ToastCenter.ProgressRequested+=vm=>{Toasts.Add(vm);vm.PropertyChanged+=async(_,e)=>{if(e.PropertyName=="IsRunning"&&!vm.IsRunning){await Task.Delay(2200);Toasts.Remove(vm);}};};}
    public void SetForeground(bool foreground) { }
    public void SelectTab(NavItem? tab){if(tab==null)return;SelectLeaf(tab.HasChildren?tab.Children.First():tab);}
    private NavItem Leaf(string title,string glyph,Func<UserControl> factory){var item=new NavItem(title,glyph,factory);item.Selected+=SelectLeaf;_leaves.Add(item);return item;}
    private void SelectLeaf(NavItem leaf){if(!_leaves.Contains(leaf))return;var tab=Nav.First(n=>n==leaf||n.Children.Contains(leaf));SelectedTab=tab;foreach(var item in Nav)item.RowTag=item==tab?"active":"";foreach(var item in _leaves)item.RowTag=item==leaf?"active":"";SubNav.Clear();foreach(var child in tab.Children)SubNav.Add(NavRow.Leaf(child));ActiveContent=leaf.View;HeaderTitle=leaf.Title;Raise(nameof(HasSubNav));Raise(nameof(SectionTitle));Raise(nameof(ShowDdu));if(ShowDdu)Ddu.Refresh();}
    public bool NavigateToKey(string title){var item=_leaves.FirstOrDefault(n=>n.Title==title);if(item==null)return false;SelectLeaf(item);return true;}
    private static Geometry GpuIcon()=>Geometry.Parse("M2,4 H16 V13 H2 Z M1,2 V15 M5,13 V16 M8,13 V16 M11,13 V16 M8,6 A2.5,2.5 0 1 1 8,11 A2.5,2.5 0 1 1 8,6");
}
