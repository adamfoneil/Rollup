using System.Data;

namespace RollupLibrary.Interfaces;

public interface IMarkerRepository
{
	Task<IMarker> GetOrCreateAsync(IDbConnection connection, string name);
	Task SaveAsync(IDbConnection connection, IMarker marker);
}
