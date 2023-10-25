namespace RollupLibrary.Models;

public record Marker
{
	public string Name { get; set; } = default!;
	public long Version { get; set; }
	public DateTime LastSyncUtc { get; set; }
}
