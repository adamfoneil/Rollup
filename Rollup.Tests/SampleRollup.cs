using Dapper;
using Microsoft.Extensions.Logging;
using Rollup.Tests.Entities;
using System.Data;
using RollupLibrary.Extensions;

namespace Rollup.Tests;

internal class SampleRollup : RollupLibrary.Rollup<Marker>
{
	public SampleRollup(MarkerRepo repo, ILogger<RollupLibrary.Rollup<Marker>> logger) : base(repo, logger)
	{
	}

	protected override string MarkerName => "sales";

	protected override async Task<int> OnExecuteAsync(IDbConnection connection, long sinceVersion)
	{
		return await new SalesTable().MergeAsync(connection, sinceVersion);

		// if there were more rollup tables, you'd call their MergeAsync methods here
	}

	private class SalesTable : Table<SalesRollup, SalesRollupKey>
	{
		protected override string TableName => "dbo.SalesRollup";

		/// <summary>
		/// implicit conversion operator performs the conversion
		/// </summary>
		protected override SalesRollupKey GetKey(SalesRollup entity) => entity;

		protected override async Task<IEnumerable<SalesRollup>> QueryChangesAsync(IDbConnection connection, long sinceVersion) =>
			await connection.QueryAsync<SalesRollup>(
				@"WITH [dimensions] AS (
					SELECT
						[s].[RegionId],
						[i].[Type] AS [ItemType],						
						YEAR([s].[Date]) AS [Year]
					FROM
						CHANGETABLE(changes [dbo].[DetailSalesRow], @sinceVersion) [c]
						INNER JOIN [dbo].[DetailSalesRow] [s] ON [c].[Id]=[s].[Id]
						INNER JOIN [dbo].[Item] [i] ON [s].[ItemId]=[i].[Id]
						INNER JOIN [dbo].[Region] [r] ON [s].[RegionId]=[r].[Id]
					GROUP BY
						[s].[RegionId],
						[i].[Type],
						YEAR([s].[Date])
				) SELECT
					[r].[Name] AS [Region],
					[dim].[ItemType],
					[dim].[Year],
					SUM([Price]) AS [Total]
				FROM
					[dbo].[DetailSalesRow] [fact]
					INNER JOIN [dbo].[Item] [i] ON [fact].[ItemId]=[i].[Id]
					INNER JOIN [dbo].[Region] [r] ON [fact].[RegionId]=[r].[Id]
					INNER JOIN [dimensions] [dim] ON
						[fact].[RegionId]=[dim].[RegionId] AND
						[i].[Type]=[dim].[ItemType] AND
						YEAR([fact].[Date])=[dim].[Year]
				GROUP BY
					[r].[Name],
					[dim].[ItemType],
					[dim].[Year]", new { sinceVersion });		
	}
}
