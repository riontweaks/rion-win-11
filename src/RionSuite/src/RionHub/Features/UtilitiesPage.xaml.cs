using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RionHub.Modules.Tools.Windows;
using RionHub.Shell;

namespace RionHub.Features;

public partial class UtilitiesPage : UserControl
{
    public static CancellationTokenSource Preparation = new();
    private readonly List<UtilityLibraryRow> rows = [];
    public UtilitiesPage()
    {
        InitializeComponent();
        rows.AddRange(UtilityPackage.Entries.Select(e=>new UtilityLibraryRow(e)));
        rows.AddRange(UtilityToolCatalog.All.Select(e=>new UtilityLibraryRow(e)));
        rows.Add(UtilityLibraryRow.SevenZip());
        Category.ItemsSource=new[]{"All tools"}.Concat(UtilityLibraryRow.Categories);
        Category.SelectedIndex=0;
        Loaded+=(_,_)=>{UtilityPackage.Changed+=Update;Update();};
        Unloaded+=(_,_)=>UtilityPackage.Changed-=Update;
        Filter();
    }
    private void Filter_Changed(object sender,TextChangedEventArgs e)=>Filter();
    private void Category_Changed(object sender,SelectionChangedEventArgs e)=>Filter();
    private void Filter()
    {
        if(Groups==null||Category==null)return;
        SearchHint.Visibility=Search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
        var visible=rows.Where(r=>(Category.SelectedIndex<=0||r.Category==Category.SelectedItem as string)&&(r.Name.Contains(Search.Text,StringComparison.OrdinalIgnoreCase)||r.Description.Contains(Search.Text,StringComparison.OrdinalIgnoreCase))).ToArray();
        Groups.ItemsSource=UtilityLibraryRow.Categories.Select(c=>new {Name=c,Rows=visible.Where(r=>r.Category==c).OrderBy(r=>r.Name).ToArray()}).Where(g=>g.Rows.Length>0).ToArray();
        NoResults.Visibility=visible.Length==0?Visibility.Visible:Visibility.Collapsed;
    }
    private void Update()=>Dispatcher.BeginInvoke(()=>
    {
        PackageStatus.Text=UtilityPackage.Status;PackageStatus.ToolTip=PackageStatus.Text;
        foreach(var row in rows)row.Refresh();
    });
    private async void Backend_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(sender is ComboBox {DataContext:UtilityLibraryRow row,SelectedItem:string program} && program!=row.Backend)
            await row.ChangeBackendAsync(program);
    }
    private void Downloads_Click(object sender,RoutedEventArgs e){var b=(Button)sender;b.ContextMenu.PlacementTarget=b;b.ContextMenu.Placement=PlacementMode.Bottom;b.ContextMenu.IsOpen=true;}
    private async void Prepare_Click(object sender,RoutedEventArgs e)
    {
        try{if(Preparation.IsCancellationRequested)Preparation=new();await UtilityPackage.PrepareAsync(Preparation.Token);}
        catch(Exception ex){PackageStatus.Text=ex.Message;}
    }
    private void Cancel_Click(object sender,RoutedEventArgs e)=>Preparation.Cancel();
    private void Folder_Click(object sender,RoutedEventArgs e)
    {
        try{if(!Directory.Exists(UtilityPackage.Root))throw new IOException("The utility package is not ready yet.");Process.Start(new ProcessStartInfo(UtilityPackage.Root){UseShellExecute=true});}
        catch(Exception ex){PackageStatus.Text=ex.Message;}
    }
}

public sealed class UtilityLibraryRow : ObservableObject
{
    public static readonly string[] Categories=["Benchmarks & stress tests","System information","Diagnostics & monitoring","Drivers & hardware","Maintenance & privacy","File tools"];
    private readonly UtilityEntry? package;
    private readonly UtilityToolRowViewModel? original;
    private readonly bool sevenZip;
    private bool busy;
    private string status="";
    private string? backend;
    private string? configHash;
    public string Name {get;}
    public string Description {get;}
    public string Category {get;}
    public bool IsCoreCycler=>Name=="CoreCycler";
    public string[] Programs=>CoreCyclerConfig.Programs;
    public string? Backend=>backend;
    public bool CanConfigure=>IsCoreCycler&&!busy&&configHash!=null;
    public string Status=>original?.Status??status;
    public string LocationHint=>original?.LocationHint??(sevenZip?SevenZipPath:package==null?"":UtilityPackage.Resolve(package.EntryPoint));
    public string ActionLabel=>original?.ActionLabel??(busy?"Working…":File.Exists(LocationHint)?"Run":"Get");
    public RelayCommand GetCommand {get;}
    private string Config=>UtilityPackage.Resolve("CoreCycler-v0.11.0.3/config.ini");
    private static string SevenZipPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"7-Zip","7zFM.exe");
    public UtilityLibraryRow(UtilityEntry entry)
    {
        package=entry;Name=entry.Name;
        (Category,Description)=Name switch
        {
            "Cinebench R23"=>(Categories[0],"Measure CPU rendering performance."),
            "CoreCycler"=>(Categories[0],"Test CPU cores with your selected stress test program."),
            "RAM Test Pro"=>(Categories[0],"Check memory stability with RAM Test Pro."),
            "TestMem5 / TM5"=>(Categories[0],"Test memory stability with your TM5 configuration."),
            "CPU-Z"=>(Categories[1],"CPU, motherboard, memory, and SPD information."),
            "HWiNFO"=>(Categories[1],"Hardware information and live sensor monitoring."),
            "Thaiphoon Burner"=>(Categories[1],"Inspect memory modules and SPD information."),
            "ZenTimings"=>(Categories[1],"Inspect AMD memory timings and related voltages."),
            _=>(Categories[3],"Inspect and control supported AMD SMU settings.")
        };
        GetCommand=new RelayCommand(async()=>await RunAsync(),()=>!busy);Refresh();
    }
    public UtilityLibraryRow(UtilityToolEntry entry)
    {
        original=new(entry);Name=entry.Name;Description=entry.Description;
        Category=Name switch
        {
            "Process Explorer" or "TCPView"=>Categories[2],
            "Autoruns" or "Windows Update Blocker (WUB)" or "O&O ShutUp10"=>Categories[4],
            _=>Categories[3]
        };
        GetCommand=original.GetCommand;
        original.PropertyChanged+=(_,e)=>{if(e.PropertyName!=null)Raise(e.PropertyName);};
    }
    private UtilityLibraryRow(){sevenZip=true;Name="7-Zip";Description="Compress and extract archives.";Category=Categories[5];GetCommand=new RelayCommand(async()=>await RunAsync(),()=>!busy);}
    public static UtilityLibraryRow SevenZip()=>new();
    private void Report(string message){status=message;Raise(nameof(Status));}
    private void SetBusy(bool value){busy=value;Raise(nameof(CanConfigure));Raise(nameof(ActionLabel));GetCommand.RaiseCanExecuteChanged();}
    public void Refresh()
    {
        Raise(nameof(ActionLabel));Raise(nameof(LocationHint));
        if(!IsCoreCycler||busy)return;
        try
        {
            if(!File.Exists(Config)){configHash=null;backend=null;}
            else{var bytes=File.ReadAllBytes(Config);configHash=Convert.ToHexString(SHA256.HashData(bytes));backend=CoreCyclerConfig.Read(File.ReadAllText(Config));}
        }
        catch(Exception ex){configHash=null;backend=null;Report(ex.Message);}
        Raise(nameof(Backend));Raise(nameof(CanConfigure));
    }
    public async Task ChangeBackendAsync(string selected)
    {
        if(busy||!IsCoreCycler||selected==backend)return;
        SetBusy(true);
        try
        {
            if(configHash==null)throw new InvalidOperationException("CoreCycler is not ready yet.");
            var running=await ShellRunner.PowerShellAsync("$p=@(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.Name -match '^(powershell|pwsh)\\.exe$' -and $_.CommandLine -match 'script-corecycler\\.ps1' }); if($p.Count){exit 7}",null,15000);
            if(running.ExitCode!=0)throw new InvalidOperationException("Stop CoreCycler before changing its stress test program. Process inspection must succeed.");
            await Task.Run(()=>CoreCyclerConfig.Save(Config,selected,configHash));
            backend=selected;Report("Saved · "+selected);
        }
        catch(Exception ex){Report(ex.Message);}
        finally{SetBusy(false);Refresh();}
    }
    private async Task RunAsync()
    {
        SetBusy(true);
        try
        {
            if(sevenZip&&!File.Exists(SevenZipPath))
            {
                var ok=await WingetInstallService.InstallAsync("7zip.7zip",line=>System.Windows.Application.Current.Dispatcher.BeginInvoke(()=>Report(line)),default,true);
                Report(ok?"Installed. Use Run to open 7-Zip.":"Installation did not complete.");return;
            }
            if(package!=null&&!File.Exists(LocationHint))
            {
                if(UtilitiesPage.Preparation.IsCancellationRequested)UtilitiesPage.Preparation=new();
                await UtilityPackage.PrepareAsync(UtilitiesPage.Preparation.Token);Report("Ready. Use Run to launch.");return;
            }
            if(!File.Exists(LocationHint))throw new FileNotFoundException("The program is not available yet.");
            Process.Start(new ProcessStartInfo(LocationHint){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(LocationHint)});
            Report("Launched.");
        }
        catch(Exception ex){Report(ex.Message);}
        finally{SetBusy(false);Refresh();}
    }
}
