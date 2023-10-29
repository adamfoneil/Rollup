using RollupLibrary.Extensions;

namespace Rollup.Tests;

[TestClass]
public class SqlMany
{
	[TestMethod]
	public async Task InsertAndDeleteMany()
	{
		using var cn = Util.InitDatabase(
			"Rollup.Tests.Resources.RollupDemo.bacpac",
			"Server=(localdb)\\mssqllocaldb;Database=RollupDemo;Integrated Security=true");

		var items = new Item[]
		{
			new Item() { Name = "whatever", Type = "anything" },
			new Item() { Name = "bogus", Type = "johnson" },
			new Item() { Name = "junebug", Type = "thorium" },
			new Item() { Name = "thalamus", Type = "isthmus" },
			new Item() { Name = "cropsie", Type = "hydro" },
			new Item() { Name = "thrombus", Type = "julior" },
			new Item() { Name = "horpkin", Type = "vyle" },
			new Item() { Name = "relly", Type = "stanzig" },
		};

		var count = await cn.InsertManyAsync("dbo.Item", items);
		Assert.AreEqual(items.Length, count);

		var keys = items.Select(i => new { i.Name });

		count = await cn.DeleteManyAsync("dbo.Item", keys);
		Assert.AreEqual(items.Length, count);
	}

	public class Item
	{
		public int Id { get; set; }
		public string Name { get; set; } = default!;
		public string Type { get; set; } = default!;
	}
}
