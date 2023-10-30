namespace Rollup.Tests.Entities;

internal class SalesRollup
{
	public int Id { get; set; }
	public string Region { get; set; } = default!;
	public string ItemType { get; set; } = default!;
	public int Year { get; set; }
	/// <summary>
	/// this is the "fact" column in the rollup
	/// </summary>
	public decimal Total { get; set; }

	public static implicit operator SalesRollupKey(SalesRollup entity) => new()
	{
		Region = entity.Region,
		ItemType = entity.ItemType,
		Year = entity.Year
	};
}

/// <summary>
/// this is, in effect, your "dimension" columns
/// </summary>
internal record SalesRollupKey
{
	public string Region { get; set; } = default!;
	public string ItemType { get; set; } = default!;
	public int Year { get; set; }
}
