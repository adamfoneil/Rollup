using Dapper;
using RollupLibrary.Interfaces;
using RollupLibrary.Models;
using System.Data;

namespace Rollup.Tests;

internal class MarkerRepo : IMarkerRepository
{
	public async Task<Marker> GetOrCreateAsync(IDbConnection connection, string name) =>
		await connection.QuerySingleOrDefaultAsync<Marker>(
			"SELECT * FROM [dbo].[ChangeTrackingMarker] WHERE [Name]=@name", new { name }) ?? 
		new Marker() { Name = name };
	

	public Task SaveAsync(IDbConnection connection, Marker marker)
	{
		throw new NotImplementedException();
	}
}
