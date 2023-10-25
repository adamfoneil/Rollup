using RollupLibrary.Models;
using System.Data;

namespace RollupLibrary.Interfaces;

public interface IMarkerRepository
{
	Task<Marker> GetOrCreateAsync(IDbConnection connection, string name);
	Task SaveAsync(IDbConnection connection, Marker marker);
}
