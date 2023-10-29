using Bogus;
using Dapper;
using Microsoft.Extensions.Logging;
using Rollup.Tests.Entities;
using RollupLibrary.Extensions;
using System.Data;

namespace Rollup.Tests;

[TestClass]
public class Integration
{
	[TestMethod]
	public async Task StandardCase()
	{
		using var cn = Util.InitDatabase(
			"Rollup.Tests.Resources.RollupDemo.bacpac",
			"Server=(localdb)\\mssqllocaldb;Database=RollupDemo;Integrated Security=true");

		var repo = new MarkerRepo();
		var logger = LoggerFactory.Create(config => config.AddDebug()).CreateLogger<SampleRollup>();
		var rollup = new SampleRollup(repo, logger);

		// assume 4 rounds of random changes
		for (int i = 0; i < 4; i++)
		{
			await CreateSampleDataAsync(cn, 100);
			await rollup.ExecuteAsync(cn);

			var dynamicResults = (await cn.QueryAsync<SalesRollup>(
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
					YEAR([s].[Date])")).ToDictionary(row => (row.Region, row.ItemType, row.Year), row => row.Total);

			// the dynamic results should match the rollup data
			var rollupResults = (await cn.QueryAsync<SalesRollup>(
				@"SELECT
					[Region],
					[ItemType],
					[Year],
					[Total]
				FROM
					[dbo].[SalesRollup] [sr]")).ToDictionary(row => (row.Region, row.ItemType, row.Year), row => row.Total);

			Assert.IsTrue(dynamicResults.All(kp => rollupResults[kp.Key].Equals(kp.Value)));
		}
	}

	private async Task CreateSampleDataAsync(IDbConnection cn, int count)
	{
		var regionIds = (await cn.QueryAsync<int>("SELECT [Id] FROM [dbo].[Region]")).ToArray();
		var itemIds = (await cn.QueryAsync<int>("SELECT [Id] FROM [dbo].[Item]")).ToArray();

		var rows = new Faker<DetailSalesRow>()
			.RuleFor(row => row.ItemId, bogus => bogus.PickRandom(itemIds))
			.RuleFor(row => row.RegionId, bogus => bogus.PickRandom(regionIds))
			.RuleFor(row => row.Date, bogus => bogus.Date.Between(new DateTime(2020, 1, 1), DateTime.Today))
			.RuleFor(row => row.Price, bogus => bogus.Random.Decimal(5, 35))
			.Generate(count);

		await cn.InsertManyAsync("dbo.DetailSalesRow", rows);
	}
}