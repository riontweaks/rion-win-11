using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Features;

public sealed class DriverDevice
{
    public string Name {get;set;}="";
    public string Id {get;set;}="";
    public string[] HardwareIds {get;set;}=[];
    public string[] CompatibleIds {get;set;}=[];
    public string Class {get;set;}="";
    public string Provider {get;set;}="";
    public string Version {get;set;}="";
    public string Inf {get;set;}="";
    public int Problem {get;set;}
    public string Status {get;set;}="Not checked";
    public string? UpdateId {get;set;}
    public string? UpdateTitle {get;set;}
    public string DisplayName=>!string.IsNullOrWhiteSpace(Name)?Name:!string.IsNullOrWhiteSpace(Id)?Id:"Unknown device";
    public string DisplayVersion=>string.IsNullOrWhiteSpace(Version)?"Not reported":Version;
}
public sealed record DriverUpdate(string Id,string Title,string HardwareId);
public static class DriverInventory
{
    public const string ScanScript = """
        $ErrorActionPreference='Stop'
        $drivers=@{}; Get-CimInstance Win32_PnPSignedDriver | ForEach-Object { if($_.DeviceID){$drivers[$_.DeviceID]=$_} }
        $result=@(Get-CimInstance Win32_PnPEntity | ForEach-Object {
            $d=$drivers[$_.DeviceID]; $problem=[int]$_.ConfigManagerErrorCode
            [pscustomobject]@{Name=[string]$_.Name;Id=[string]$_.DeviceID;HardwareIds=@($_.HardwareID | Where-Object {$_});CompatibleIds=@($_.CompatibleID | Where-Object {$_});Class=[string]$_.PNPClass;Provider=[string]$d.DriverProviderName;Version=[string]$d.DriverVersion;Inf=[string]$d.InfName;Problem=$problem;Status=$(if($problem -eq 28){'Driver missing'}elseif($problem -ne 0){'Device problem'}else{'Not checked'})}
        })
        'RIONJSON:'+ (ConvertTo-Json -InputObject $result -Depth 5 -Compress)
        """;
    public const string UpdatesScript = """
        $ErrorActionPreference='Stop'
        $session=New-Object -ComObject Microsoft.Update.Session
        $result=$session.CreateUpdateSearcher().Search("IsInstalled=0 and Type='Driver' and IsHidden=0")
        $items=@(for($i=0;$i -lt $result.Updates.Count;$i++){$u=$result.Updates.Item($i);[pscustomobject]@{Id=$u.Identity.UpdateID;Title=$u.Title;HardwareId=$u.DriverHardwareID}})
        'RIONJSON:'+(ConvertTo-Json -InputObject $items -Compress)
        """;
    public static async Task<T[]> Read<T>(string script,int timeout=60000)
    {
        var result=await ShellRunner.PowerShellAsync(script,null,timeout);
        if(result.ExitCode!=0||result.TimedOut)throw new IOException(result.Output);
        string? json=result.Output.Split('\n').LastOrDefault(l=>l.StartsWith("RIONJSON:"));
        return JsonSerializer.Deserialize<T[]>(json?[9..]??throw new IOException("Inventory output missing."))??[];
    }
    public static void MatchUpdates(IEnumerable<DriverDevice> devices,IEnumerable<DriverUpdate> updates)
    {
        foreach(var device in devices)
        {
            var matches=updates.Where(u=>device.HardwareIds.Concat(device.CompatibleIds).Contains(u.HardwareId,StringComparer.OrdinalIgnoreCase)).ToArray();
            device.UpdateId=null;device.UpdateTitle=null;
            if(matches.Length==1){device.UpdateId=matches[0].Id;device.UpdateTitle=matches[0].Title;device.Status="Compatible update available";}
            else if(matches.Length>1)device.Status="Multiple candidates — review in Windows Update";
            else if(device.Problem==0)device.Status="No update found in Windows Update";
        }
    }
}
public sealed class DriversPage:FeaturePage
{
    private DriverDevice[] devices=[];
    private readonly Func<Task<DriverDevice[]>> scanDevices;
    private readonly Func<Task<DriverUpdate[]>> findUpdates;
    private readonly Button checkUpdates;
    private readonly Button applyUpdates;
    private bool scanned;
    private readonly DataGrid table=Table(("Device","DisplayName"),("Installed version","DisplayVersion"),("Status","Status"));
    private readonly TextBox search=new(){Margin=new Thickness(0,0,0,8),Height=34,Padding=new Thickness(10,4,10,4),HorizontalAlignment=HorizontalAlignment.Left,ToolTip="Filter devices by name, ID or status"};
    private readonly TextBox details=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    public DriversPage():this(()=>DriverInventory.Read<DriverDevice>(DriverInventory.ScanScript),()=>DriverInventory.Read<DriverUpdate>(DriverInventory.UpdatesScript,300000)) { }
    public DriversPage(Func<Task<DriverDevice[]>> scanDevices, Func<Task<DriverUpdate[]>> findUpdates):base("Drivers","Check Windows Update for compatible device drivers.")
    {
        this.scanDevices=scanDevices; this.findUpdates=findUpdates;
        checkUpdates=Action("Check for updates",()=>RunOperation(CheckUpdates));
        applyUpdates=Action("Apply updates",()=>RunOperation(Install));
        applyUpdates.Visibility=Visibility.Collapsed;
        Action("Device Manager / rollback",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("devmgmt.msc"){UseShellExecute=true}));
        Header.Children.Add(new TextBlock {Text="Search devices",Margin=new Thickness(0,0,0,4)});
        Header.Children.Add(search);
        Header.SizeChanged+=(_,e)=>search.Width=Math.Min(420,e.NewSize.Width);
        var cellText=new Style(typeof(TextBlock),(Style)FindResource(typeof(TextBlock)));
        cellText.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
        foreach(var column in table.Columns.OfType<DataGridTextColumn>())column.ElementStyle=cellText;
        table.Columns[0].Width=new DataGridLength(2,DataGridLengthUnitType.Star);
        var inspector=new Expander {Header="Device details",Content=details,Margin=new Thickness(0,0,0,8),Visibility=Visibility.Collapsed};
        Header.Children.Add(inspector);Layout.Children.Add(table);
        search.TextChanged+=(_,_)=>Refresh();table.SelectionChanged+=(_,_)=>{inspector.Visibility=table.SelectedItem is DriverDevice?Visibility.Visible:Visibility.Collapsed;if(table.SelectedItem is DriverDevice d)details.Text=$"Class: {d.Class}; provider: {d.Provider}\nDevice instance: {d.Id}\nHardware IDs: {string.Join(", ",d.HardwareIds)}\nCompatible IDs: {string.Join(", ",d.CompatibleIds)}\nINF: {d.Inf}; problem code: {d.Problem}\nCandidate: {d.UpdateTitle??"None"}";};
        Loaded+=async(_,_)=>{if(scanned||Busy)return; Busy=true;try{await RunOperation(Scan);}catch(Exception ex){Status.Text="Device scan failed. Check for updates to retry. "+ex.Message;}finally{Busy=false;}};
    }
    private async Task RunOperation(Func<Task> work)
    {
        checkUpdates.IsEnabled=false;applyUpdates.IsEnabled=false;
        try{await work();}finally{checkUpdates.IsEnabled=true;applyUpdates.IsEnabled=true;UpdateActions();}
    }
    private void UpdateActions(){int count=PendingUpdates().Length;applyUpdates.Content=$"Apply updates ({count})";applyUpdates.Visibility=count>0?Visibility.Visible:Visibility.Collapsed;}
    private DriverDevice[] PendingUpdates()=>devices.Where(d=>Guid.TryParse(d.UpdateId,out _)).GroupBy(d=>d.UpdateId,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
    private async Task CheckUpdates(){if(!scanned)await Scan();foreach(var d in devices){d.UpdateId=null;d.UpdateTitle=null;}UpdateActions();Status.Text="Checking Windows Update…";var updates=await findUpdates();DriverInventory.MatchUpdates(devices,updates);Refresh();Status.Text=$"{PendingUpdates().Length} compatible updates found · Checked {DateTime.Now:t}. Windows Update may not offer every vendor's latest release.";}
    private async Task Scan(){Status.Text="Scanning devices…";devices=await scanDevices();scanned=true;Refresh();Status.Text=$"{devices.Length} devices · Ready to check for updates.";}
    private void Refresh(){table.ItemsSource=devices.Where(d=>(d.DisplayName+" "+d.Id+" "+d.Status).Contains(search.Text,StringComparison.OrdinalIgnoreCase)).OrderByDescending(d=>d.UpdateId!=null).ThenBy(d=>d.DisplayName,StringComparer.CurrentCultureIgnoreCase).ToArray();UpdateActions();}
    private async Task Install()
    {
        var pending=PendingUpdates(); if(pending.Length==0)return;
        if(MessageBox.Show("Install these compatible Windows Update drivers?\n\n"+string.Join("\n",pending.Select(d=>d.UpdateTitle))+"\n\nThis accepts their license terms. Connectivity may be interrupted and a restart may be required. Driver backups are attempted where supported; rollback is not guaranteed.","Review driver updates",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        var results=new List<string>();
        try{foreach(var device in pending){await InstallDevice(device);results.Add(Status.Text);}}
        catch(Exception ex){throw new IOException($"{results.Count} of {pending.Length} updates completed. Check for updates before retrying.\n"+string.Join("\n",results)+"\n"+ex.Message,ex);}
        finally{foreach(var device in devices){device.UpdateId=null;device.UpdateTitle=null;}Refresh();}
        await Scan();Status.Text=string.Join("\n",results);
    }
    private async Task InstallDevice(DriverDevice device)
    {
        if(!Guid.TryParse(device.UpdateId,out var id))throw new InvalidOperationException("Check updates again.");
        string folder=Path.Combine(UtilityPackage.DataRoot,"DriverHistory",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+id.ToString("N"));Directory.CreateDirectory(folder);
        var affected=devices.Where(d=>string.Equals(d.UpdateId,device.UpdateId,StringComparison.OrdinalIgnoreCase)).ToArray();
        await File.WriteAllTextAsync(Path.Combine(folder,"before.json"),JsonSerializer.Serialize(affected));
        foreach(var inf in affected.Select(d=>d.Inf).Distinct(StringComparer.OrdinalIgnoreCase).Where(inf=>System.Text.RegularExpressions.Regex.IsMatch(inf,@"^oem\d+\.inf$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
        {
            var backup=await ShellRunner.RunAsync(Path.Combine(Environment.SystemDirectory,"pnputil.exe"),new[]{"/export-driver",inf,folder},null,60000);
            await File.WriteAllTextAsync(Path.Combine(folder,inf+"-backup.txt"),backup.Output);
            if(backup.ExitCode!=0||backup.TimedOut)throw new IOException("Previous driver export failed. Installation stopped; inspect "+folder);
        }
        string script="""
            $ErrorActionPreference='Stop'
            $session=New-Object -ComObject Microsoft.Update.Session
            $found=$session.CreateUpdateSearcher().Search("IsInstalled=0 and Type='Driver'")
            $selected=@($found.Updates | Where-Object {$_.Identity.UpdateID -eq '__ID__'})
            if($selected.Count -ne 1){throw 'Update applicability changed. Check updates again.'}
            $u=$selected[0];if(-not $u.EulaAccepted){$u.AcceptEula()}
            $collection=New-Object -ComObject Microsoft.Update.UpdateColl;$null=$collection.Add($u)
            $download=$session.CreateUpdateDownloader();$download.Updates=$collection;$result=$download.Download()
            if($result.ResultCode -ne 2){throw 'Driver download did not complete successfully.'}
            $installer=$session.CreateUpdateInstaller();$installer.Updates=$collection;$result=$installer.Install()
            'ResultCode='+$result.ResultCode+'; RebootRequired='+$result.RebootRequired
            if($result.ResultCode -ne 2){exit 1}
            """;
        Status.Text="Downloading and installing selected driver…";
        var installed=await ShellRunner.PowerShellAsync(script.Replace("__ID__",id.ToString()),null,1800000);
        await File.WriteAllTextAsync(Path.Combine(folder,"installation.txt"),installed.Output);
        if(installed.ExitCode!=0||installed.TimedOut)throw new IOException("Installation failed or is unverified. Inspect "+folder+"\n"+installed.Output);
        var after=(await scanDevices()).FirstOrDefault(d=>d.Id==device.Id);
        Status.Text=installed.Output+"\n"+(after==null?"Device not currently found; effective driver unverified.":$"Read-back version: {after.Version}; problem code {after.Problem}. A restart may still be required.")+"\nRecovery record: "+folder;
    }
}
