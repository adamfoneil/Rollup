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

		Assert.IsTrue(result.Count() == 2);
		Assert.IsTrue(result.Any(item => item.Type == MismatchType.NotInRollup && item.Data.Id == 6 && item.Data.Value == "Grouper"));
		Assert.IsTrue(result.Any(item => item.Type == MismatchType.NotInTransactional && item.Data.Id == 3 && item.Data.Value == "Whatever"));
	}
}

internal class SampleData
{
	public int Id { get; set; }
	public string Value { get; set; } = default!;
}


internal class SampleMismatchFinder : MismatchFinder<SampleData, int>
{
	protected override int GetIdentity(SampleData result) => result.Id;
	
	protected override async Task<IEnumerable<SampleData>> QueryRollupAsync(IDbConnection connection)
	{
		await Task.CompletedTask;

		return new SampleData[]
		{
			new() { Id = 1, Value = "Hello" },
			new() { Id = 2, Value = "Goodbye" },
			new() { Id = 3, Value = "Whatever" },
			new() { Id = 4, Value = "Astyanax" },
			new() { Id = 5, Value = "Ozymandias" },
		};
	}

	protected override async Task<IEnumerable<SampleData>> QueryTransactionalAsync(IDbConnection connection)
	{
		await Task.CompletedTask;

		return new SampleData[]
		{
			new() { Id = 1, Value = "Hello" },
			new() { Id = 2, Value = "Goodbye" },			
			new() { Id = 4, Value = "Astyanax" },
			new() { Id = 5, Value = "Ozymandias" },
			new() { Id = 6, Value = "Grouper" }
		};
	}
}
