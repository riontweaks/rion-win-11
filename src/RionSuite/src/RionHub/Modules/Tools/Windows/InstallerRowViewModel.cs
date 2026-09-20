namespace RionHub.Modules.Tools.Windows;

public abstract class InstallerRowViewModel : ObservableObject
{
	private string _iconPath = "";

	public abstract string Name { get; }

	public string IconInitials => InstallerIcons.Initials(Name);

	public virtual string IconGlyph => "";

	public string IconPath
	{
		get
		{
			return _iconPath;
		}
		internal set
		{
			Set(ref _iconPath, value, "IconPath");
		}
	}
}
