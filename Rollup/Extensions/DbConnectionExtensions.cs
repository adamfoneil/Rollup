﻿using Dapper;
using System.Data;
using System.Text.Json;

namespace RollupLibrary.Extensions;

public static class DbConnectionExtensions
{
	public const int DefaultChunkSize = 30;
	public const string DefaultIdProperty = "Id";

	public static async Task<int> DeleteManyAsync<TKey>(
		this IDbConnection connection, string tableName, IEnumerable<TKey> keys, int chunkSize = DefaultChunkSize,
		IDbTransaction? transaction = null)
	{
		var mapping = GetSqlMapping<TKey>();
		var columnDefs = string.Join(", ", mapping.Select(col => $"[{col.ColumnName}] {col.SqlDefinition}"));
		var join = string.Join(" AND ", mapping.Select(col => $"[t].[{col.ColumnName}] = [del].[{col.ColumnName}]"));
		var sql = $"DELETE [t] FROM {tableName} AS [t] INNER JOIN OPENJSON(@json) WITH ({columnDefs}) AS [del] ON {join}";

		int result = 0;
		foreach (var chunk in keys.Chunk(chunkSize))
		{
			var json = JsonSerializer.Serialize(chunk);
			result += await connection.ExecuteAsync(sql, new { json }, transaction);
		}
		return result;
	}

	public static async Task<int> InsertManyAsync<TEntity>(
		this IDbConnection connection, string tableName, IEnumerable<TEntity> entities, int chunkSize = DefaultChunkSize,
		IDbTransaction? transaction = null)
	{
		var mapping = GetSqlMapping<TEntity>();
		var columnNames = string.Join(", ", mapping.Select(col => col.ColumnName));
		var columnDefs = string.Join(", ", mapping.Select(col => $"[{col.ColumnName}] {col.SqlDefinition}"));
		var sql = $"INSERT INTO {tableName} ({columnNames}) SELECT {columnNames} FROM OPENJSON(@json) WITH ({columnDefs})";

		int result = 0;
		foreach (var chunk in entities.Chunk(chunkSize))
		{
			var json = JsonSerializer.Serialize(chunk);
			result += await connection.ExecuteAsync(sql, new { json }, transaction);
		}
		return result;
	}

	/// <summary>
	/// Executes a query with an inner join to an OPENJSON result set.
	/// Use the token %json% in your query to indicate where the OPENJSON call is inserted.
	/// The OPENJSON call is aliased as "[json]", which you use to form the rest of your join expression
	/// </summary>
	[Obsolete("Ended up not using this, but I'm leaving it in in case it can be re-purposed elsewhere")]
	public static async Task<IEnumerable<TResult>> QueryWithArrayJoinAsync<TResult, TArray>(
		this IDbConnection connection, string queryTemplate, IEnumerable<TArray> joinItems, int chunkSize = DefaultChunkSize,
		IDbTransaction? transaction = null)
	{
		var mapping = GetSqlMapping<TArray>();
		var columnDefs = string.Join(", ", mapping.Select(col => $"[{col.ColumnName}] {col.SqlDefinition}"));
		var sql = queryTemplate.Replace("%json%", $"INNER JOIN OPENJSON(@json) WITH ({columnDefs}) AS [json]");

		List<TResult> results = new();

		foreach (var chunk in joinItems.Chunk(chunkSize))
		{
			var json = JsonSerializer.Serialize(chunk);
			var data = await connection.QueryAsync<TResult>(sql, new { json }, transaction);
			results.AddRange(data);
		}

		return results;
	}

	private static IEnumerable<(string ColumnName, string SqlDefinition)> GetSqlMapping<T>() =>
		typeof(T).GetProperties().Where(p => SupportedTypes.ContainsKey(p.PropertyType) && !p.Name.Equals(DefaultIdProperty)).Select(p => (p.Name, SupportedTypes[p.PropertyType]));

	private static Dictionary<Type, string> SupportedTypes
	{
		get
		{
			List<(Type Type, string Syntax)> coreTypes = new()
			{
				(typeof(bool), "bit"),
				(typeof(int), "int"),
				(typeof(long), "bigint"),
				(typeof(short), "smallint"),
				(typeof(byte), "tinyint"),
				(typeof(DateTime), "datetime2"),
				(typeof(decimal), "money"),
				(typeof(double), "float"),
				(typeof(Guid), "uniqueidentifier")
			};

			var nullableTypes = coreTypes.Select(item => (typeof(Nullable<>).MakeGenericType(item.Type), item.Syntax));

			var result = coreTypes.Concat(nullableTypes).ToDictionary(item => item.Item1, item => item.Item2);

			result.Add(typeof(string), "nvarchar(max)");

			return result;
		}
	}
}
