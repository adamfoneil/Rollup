using Dapper;
using Rollup.Tests.Models;
using RollupLibrary.Interfaces;
using System.Data;

namespace Rollup.Tests;

internal class MarkerRepo : IMarkerRepository
{
	public async Task<IMarker> GetOrCreateAsync(IDbConnection connection, string name) =>
		await connection.QuerySingleOrDefaultAsync<IMarker>(
			"SELECT * FROM [dbo].[ChangeTrackingMarker] WHERE [Name]=@name", new { name }) ?? 
		new Marker() { Name = name };	

	public Task SaveAsync(IDbConnection connection, IMarker marker)
	{
		throw new NotImplementedException();
	}
}
