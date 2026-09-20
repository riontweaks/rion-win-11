namespace RadeonSoftwareSlimmer.Optimize;

public sealed class UtilityToolEntry
{
	public string Name { get; set; }

	public string Description { get; set; }

	public UtilityMechanism Mechanism { get; set; }

	public string Source { get; set; }

	public string FileName { get; set; }

	public string Sha256 { get; set; }

	public string ExpectedPublisher { get; set; }

	public long MinSizeBytes { get; set; } = 1024L;

	public string[] ExecutableHints { get; set; }

	public string FallbackWingetId { get; set; }
}
