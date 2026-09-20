using RadeonSoftwareSlimmer.Optimize;

namespace RionHub.Modules.Tools.General;

public sealed class PresetOptionViewModel : ObservableObject
{
	public string Label { get; }

	public RelayCommand SelectCommand { get; }

	public PresetOptionViewModel(TweakPreset preset, TweakRowViewModel owner)
	{
		Label = preset.Label;
		SelectCommand = new RelayCommand(delegate
		{
			owner.ApplyPreset(preset.Value);
		});
	}
}
