using Dapper;
using Microsoft.Extensions.Logging;
using RollupLibrary.Extensions;
using RollupLibrary.Interfaces;
using System.Data;

namespace RollupLibrary;

/// <summary>
/// encapsulates a change tracking query that lets you merge incremental changes to one or more rollup tables
/// </summary>
public abstract class Rollup
{
	private readonly IMarkerRepository MarkerRepository;
	private readonly ILogger<Rollup> Logger;

	public Rollup(IMarkerRepository markerRepository, ILogger<Rollup> logger)
	{
		MarkerRepository = markerRepository;
		Logger = logger;
	}

	protected string MarkerName { get; } = default!;

	/// <summary>
	/// call your Table.MergeAsync methods here
	/// </summary>
	protected abstract Task OnExecuteAsync(IDbConnection connection, long sinceVersion);

	public async Task ExecuteAsync(IDbConnection connection)
	{
		var marker = await MarkerRepository.GetOrCreateAsync(connection, MarkerName);

		var currentVersion = await connection.QuerySingleAsync<long>("SELECT CHANGE_TRACKING_CURRENT_VERSION()");

		try
		{
			await OnExecuteAsync(connection, currentVersion);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error executing Rollup for {rollupType}", GetType().Name);
			throw;
		}
		
		try
		{
			marker.Version = currentVersion;
			marker.LastSyncUtc = DateTime.UtcNow;
			await MarkerRepository.SaveAsync(connection, marker);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error saving Rollup marker for {rollupType}", GetType().Name);
			throw;
		}
	}

	/// <summary>
	/// represents a specific rollup table in your application. Create instances of this
	/// in your own Rollup instances -- one for each target table
	/// </summary>
	protected abstract class Table<TEntity, TKey> where TKey : struct
	{
		/// <summary>
		/// this should query a CHANGETABLE(changes {tableName}) table function, accepting a @sinceVersion parameter,
		/// and perform whatever transformation/aggregation is necessary to derive TEntity
		/// </summary>
		protected abstract Task<IEnumerable<TEntity>> QueryChangesAsync(IDbConnection connection, long sinceVersion);

		/// <summary>
		/// for successful merging, we need to know TEntity's key, 
		/// a single property or combination of properties
		/// </summary>
		protected abstract TKey GetKey(TEntity entity);

		/// <summary>
		/// returns the name of the physical table
		/// </summary>
		protected abstract string TableName { get; }

		public async Task MergeAsync(IDbConnection connection, long sinceVersion)
		{
			var changes = await QueryChangesAsync(connection, sinceVersion);

			await MergeInternalAsync(connection, changes);
		}

		private async Task MergeInternalAsync(IDbConnection connection, IEnumerable<TEntity> changes)
		{
			var keys = changes.Select(GetKey).Distinct();
			await connection.DeleteManyAsync(TableName, keys);
			await connection.InsertManyAsync(TableName, changes);
		}
	}
}