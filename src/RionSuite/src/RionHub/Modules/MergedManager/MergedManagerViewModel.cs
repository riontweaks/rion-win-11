using RadeonSoftwareSlimmer.ViewModels.Wizard;
using RadeonSoftwareSlimmer.Views;
namespace RionHub.Modules.MergedManager;
public sealed class MergedManagerViewModel : ObservableObject
{
    private WizardViewModel? wizard;
    private DriverToolContentView? view;
    public event Action? MonitorWindowRequested;
    public WizardViewModel Wizard {get{if(wizard==null){wizard=new();wizard.MonitorWindowRequested+=()=>MonitorWindowRequested?.Invoke();}return wizard;}}
    public DriverToolContentView DriverContent=>view??=new(){DataContext=Wizard};
}
