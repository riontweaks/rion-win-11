using RadeonSoftwareSlimmer.Optimize;
using RionHub.Modules.Tools.Debloat;

namespace RionHub.Modules.Tools.Windows;

public sealed class WindowsAppRowViewModel : InstallerRowViewModel
{
	private bool _isSelected;

	public WindowsAppEntry Entry { get; }

	public override string Name => Entry.Name;

	public override string IconGlyph
	{
		get
		{
			if (!(Name == "OneDrive"))
			{
				return StoreAppIconResolver.FallbackGlyph((Entry.Identifier ?? "Microsoft.WindowsStore").Split('_')[0]);
			}
			return "\ue753";
		}
	}

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

	public WindowsAppRowViewModel(WindowsAppEntry entry)
	{
		Entry = entry;
	}
}
