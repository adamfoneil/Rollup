using RollupLibrary;
using System.Data;

namespace Rollup.Tests;

[TestClass]
public class Mismatches
{
	[TestMethod]
	public async Task FindMismatches()
	{
		// this is not needed for the test, but MismatchFinder requires a connection arg
		using var cn = Util.InitDatabase(
			"Rollup.Tests.Resources.RollupDemo.bacpac",
			"Server=(localdb)\\mssqllocaldb;Database=RollupDemo;Integrated Security=true");

		var finder = new SampleMismatchFinder();
		var result = await finder.QueryAsync(cn);

		Assert.IsTrue(result.Dimensions.Count() == 2);
		Assert.IsTrue(result.Dimensions.Any(item => item.Type == MismatchType.NotInRollup && item.Data.Id == 6 && item.Data.Value == "Grouper"));
		Assert.IsTrue(result.Dimensions.Any(item => item.Type == MismatchType.NotInTransactional && item.Data.Id == 3 && item.Data.Value == "Whatever"));

		Assert.IsTrue(result.Facts.Count() == 1);
		Assert.IsTrue(result.Facts.First().TransactionalRow.Amount == 2);
		Assert.IsTrue(result.Facts.First().RollupRow.Amount == 1);
	}
}

internal class SampleData
{
	public int Id { get; set; }
	public string Value { get; set; } = default!;
	public decimal Amount { get; set; }
}

internal class SampleMismatchFinder : MismatchFinder<SampleData, int>
{
	protected override int GetIdentity(SampleData result) => result.Id;

	protected override bool FactsAreEqual(SampleData transactional, SampleData rollup) => transactional.Amount == rollup.Amount;	

	protected override async Task<IEnumerable<SampleData>> QueryRollupAsync(IDbConnection connection)
	{
		await Task.CompletedTask;

		return new SampleData[]
		{
			new() { Id = 1, Value = "Hello", Amount = 1 },
			new() { Id = 2, Value = "Goodbye", Amount = 1 },
			new() { Id = 3, Value = "Whatever", Amount = 1 },
			new() { Id = 4, Value = "Astyanax", Amount = 1 },
			new() { Id = 5, Value = "Ozymandias", Amount = 1 },
		};
	}

	protected override async Task<IEnumerable<SampleData>> QueryTransactionalAsync(IDbConnection connection)
	{
		await Task.CompletedTask;

		return new SampleData[]
		{
			new() { Id = 1, Value = "Hello", Amount = 1 },
			new() { Id = 2, Value = "Goodbye", Amount = 1 },
			new() { Id = 4, Value = "Astyanax", Amount = 1 },
			new() { Id = 5, Value = "Ozymandias", Amount = 2 },
			new() { Id = 6, Value = "Grouper" }
		};
	}
}
