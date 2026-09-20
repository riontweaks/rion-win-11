using System.ComponentModel;
using System.Globalization;
using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Modules.Tools.General;

public sealed class TweakMemberViewModel : ObservableObject
{
	private readonly TweakRowViewModel _row;

	public SystemTweak Tweak => _row.Tweak;

	public string Title => _row.Title;

	public string Location
	{
		get
		{
			if (Tweak.IsCommandBased)
			{
				return Tweak.ApplyCommand;
			}
			if (Tweak.IsCustom)
			{
				return Tweak.SourceCommand ?? "Managed by Rion";
			}
			string text = ((Tweak.DynamicKeyPath != null) ? Tweak.DynamicKeyPath() : Tweak.KeyPath);
			if (string.IsNullOrEmpty(text))
			{
				return "Unavailable";
			}
			return ((Tweak.Hive == TweakHive.CurrentUser) ? "HKCU\\" : "HKLM\\") + text;
		}
	}

	public string ValueName => Tweak.ValueName ?? "";

	public bool HasValueName => !string.IsNullOrEmpty(Tweak.ValueName);

	public string TargetLabel
	{
		get
		{
			if (!Tweak.IsCommandBased && !Tweak.IsCustom)
			{
				return Tweak.TargetLabel;
			}
			return "Enabled";
		}
	}

	public string DefaultLabel
	{
		get
		{
			if (!Tweak.DefaultValue.HasValue)
			{
				return "unset";
			}
			return Tweak.DefaultValue.Value.ToString(CultureInfo.InvariantCulture);
		}
	}

	public string TargetSummary
	{
		get
		{
			if (!Tweak.IsCommandBased && !Tweak.IsCustom)
			{
				return "set " + ValueName + " to " + TargetLabel + ", Windows default " + DefaultLabel;
			}
			return "runs " + Title;
		}
	}

	public string StateLabel => _row.StateLabel;

	public TweakMemberViewModel(TweakRowViewModel row)
	{
		_row = row;
		row.PropertyChanged += delegate(object? _, PropertyChangedEventArgs args)
		{
			string propertyName = args.PropertyName;
			if ((propertyName == "IsOn" || propertyName == "StateLabel") ? true : false)
			{
				Raise("StateLabel");
			}
		};
	}
}
