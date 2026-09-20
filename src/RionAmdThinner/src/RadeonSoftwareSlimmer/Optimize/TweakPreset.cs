namespace RadeonSoftwareSlimmer.Optimize;

public readonly struct TweakPreset
{
	public string Label { get; }

	public long Value { get; }

	public TweakPreset(string label, long value)
	{
		Label = label;
		Value = value;
	}
}
