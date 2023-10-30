namespace Rollup.Tests.Entities;

internal class SalesRollup
{
	public int Id { get; set; }
	public string Region { get; set; } = default!;
	public string ItemType { get; set; } = default!;
	public int Year { get; set; }
	public decimal Total { get; set; }

	public static implicit operator SalesRollupKey(SalesRollup entity) => new()
	{
		Region = entity.Region,
		ItemType = entity.ItemType,
		Year = entity.Year
	};
}

internal record SalesRollupKey
{
	public string Region { get; set; } = default!;
	public string ItemType { get; set; } = default!;
	public int Year { get; set; }
}
