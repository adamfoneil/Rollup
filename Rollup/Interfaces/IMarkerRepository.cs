using System.Data;

namespace RollupLibrary.Interfaces;

public interface IMarkerRepository<T> where T : IMarker
{
	Task<T> GetOrCreateAsync(IDbConnection connection, string name);
	Task SaveAsync(IDbConnection connection, T marker);
}
