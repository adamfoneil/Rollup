using Dapper;
using Dommel;
using Rollup.Tests.Entities;
using RollupLibrary.Interfaces;
using System.Data;

namespace Rollup.Tests;

internal class MarkerRepo : IMarkerRepository<Marker>
{
	public async Task SaveAsync(IDbConnection connection, Marker marker)
	{
		if (marker.Id == 0)
		{
			await connection.InsertAsync(marker);
		}
		else
		{
			await connection.UpdateAsync(marker);
		}
	}

	public async Task<Marker> GetOrCreateAsync(IDbConnection connection, string name) =>
		await connection.QuerySingleOrDefaultAsync<Marker>(
			"SELECT * FROM [dbo].[ChangeTrackingMarker] WHERE [Name]=@name", new { name }) ??
		new Marker() { Name = name };
}
