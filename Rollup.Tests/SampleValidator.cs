using Dapper;
using Rollup.Tests.Entities;
using RollupLibrary;
using System.Data;

namespace Rollup.Tests;

internal class SampleValidator : TotalsValidator<SalesRollup, SalesRollupKey, decimal>
{
	protected override decimal GetFact(SalesRollup rollup) => rollup.Total;

	protected override SalesRollupKey GetKey(SalesRollup rollup) => rollup;

	protected override async Task<IEnumerable<SalesRollup>> QueryLiveAsync(IDbConnection connection) =>
		await connection.QueryAsync<SalesRollup>(
			@"SELECT
				[r].[Name] AS [Region],
				[i].[Type] AS [ItemType],
				YEAR([s].[Date]) AS [Year],
				SUM([s].[Price]) AS [Total]
			FROM					
				[dbo].[DetailSalesRow] [s]
				INNER JOIN [dbo].[Item] [i] ON [s].[ItemId]=[i].[Id]
				INNER JOIN [dbo].[Region] [r] ON [s].[RegionId]=[r].[Id]
			GROUP BY
				[r].[Name],
				[i].[Type],					
				YEAR([s].[Date])");

	protected override async Task<IEnumerable<SalesRollup>> QueryRollupAsync(IDbConnection connection) =>
		await connection.QueryAsync<SalesRollup>(
			@"SELECT
				[Region],
				[ItemType],
				[Year],
				[Total]
			FROM
				[dbo].[SalesRollup] [sr]");
}
