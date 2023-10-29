using RollupLibrary.Interfaces;

namespace Rollup.Tests.Models;

internal class Marker : IMarker
{
	public string Name { get; set; } = default!;
	public long Version { get; set; }
	public DateTime LastSyncUtc { get; set; }
}
