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

	protected override async Task OnExecuteAsync(IDbConnection connection, long sinceVersion)
	{
		await new SalesTable().MergeAsync(connection, sinceVersion);

		// if there were more rollup tables, you'd call their MergeAsync methods here
	}

	private class SalesTable : Table<SalesRollup, SalesRollupKey>
	{
		protected override string TableName => "dbo.SalesRollup";

		/// <summary>
		/// implicit conversion operator performs the conversion
		/// </summary>
		protected override SalesRollupKey GetKey(SalesRollup entity) => entity;

		protected override async Task<IEnumerable<SalesRollupKey>> QueryKeyChangesAsync(IDbConnection connection, long sinceVersion) =>
			await connection.QueryAsync<SalesRollupKey>(
				@"SELECT
					[r].[Name] AS [Region],
					[i].[Type] AS [ItemType],
					YEAR([s].[Date]) AS [Year]
				FROM
					CHANGETABLE(changes [dbo].[DetailSalesRow], @sinceVersion) [c]
					INNER JOIN [dbo].[DetailSalesRow] [s] ON [c].[Id]=[s].[Id]
					INNER JOIN [dbo].[Item] [i] ON [s].[ItemId]=[i].[Id]
					INNER JOIN [dbo].[Region] [r] ON [s].[RegionId]=[r].[Id]
				GROUP BY
					[r].[Name],
					[i].[Type],
					YEAR([s].[Date])", new { sinceVersion });

		protected override async Task<IEnumerable<SalesRollup>> QueryRollupRowsAsync(IDbConnection connection, IEnumerable<SalesRollupKey> keyChanges) =>
			await connection.QueryWithArrayJoinAsync<SalesRollup, SalesRollupKey>(
				@"SELECT
					[r].[Name] AS [Region],
					[i].[Type] AS [ItemType],
					YEAR([s].[Date]) AS [Year],
					SUM([s].[Price]) AS [Total]
				FROM					
					[dbo].[DetailSalesRow] [s]						
					INNER JOIN [dbo].[Item] [i] ON [s].[ItemId]=[i].[Id]
					INNER JOIN [dbo].[Region] [r] ON [s].[RegionId]=[r].[Id]
					%json% ON [json].[Region]=[r].[Name] AND [json].[ItemType]=[i].[Type] AND [json].[Year]=YEAR([s].[Date])
				GROUP BY
					[r].[Name],
					[i].[Type],
					YEAR([s].[Date])", keyChanges);
	}
}
