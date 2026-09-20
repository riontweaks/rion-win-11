using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Modules.Tools.Windows;

public sealed class WingetAppRowViewModel : InstallerRowViewModel
{
	private bool _isSelected;

	public WingetAppEntry Entry { get; }

	public override string Name => Entry.Name;

	public string Description => Entry.Description;

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			Set(ref _isSelected, value, "IsSelected");
		}
	}

	public WingetAppRowViewModel(WingetAppEntry entry)
	{
		Entry = entry;
	}
}
