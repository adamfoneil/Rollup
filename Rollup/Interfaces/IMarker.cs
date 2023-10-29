namespace RollupLibrary.Interfaces;

public interface IMarker
{
	string Name { get; set; }
	long Version { get; set; }
	DateTime LastSyncUtc { get; set; }
}
