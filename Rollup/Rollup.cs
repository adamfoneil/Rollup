using Dapper;
using Microsoft.Extensions.Logging;
using RollupLibrary.Extensions;
using RollupLibrary.Interfaces;
using System.Data;

namespace RollupLibrary;

/// <summary>
/// encapsulates logic to apply incremental changes to one or more rollup tables
/// </summary>
public abstract class Rollup<TMarker> where TMarker : IMarker
{
	private readonly IMarkerRepository<TMarker> MarkerRepository;
	private readonly ILogger<Rollup<TMarker>> Logger;

	public Rollup(IMarkerRepository<TMarker> markerRepository, ILogger<Rollup<TMarker>> logger)
	{
		MarkerRepository = markerRepository;
		Logger = logger;
	}

	protected abstract string MarkerName { get; }

	public async Task<bool> HasChangesAsync(IDbConnection connection)
	{
		var marker = await MarkerRepository.GetOrCreateAsync(connection, MarkerName);
		return await HasChangesInternalAsync(connection, marker.Version);
	}

	/// <summary>
	/// use this to call your Table.HasChangesAsync method for all your Table instances within this Rollup
	/// </summary>	
	protected abstract Task<bool> HasChangesInternalAsync(IDbConnection connection, long sinceVersion);

	/// <summary>
	/// call your Table.MergeAsync methods here, returns the number of rollup rows merged
	/// </summary>
	protected abstract Task<int> OnExecuteAsync(IDbConnection connection, long sinceVersion);

	public async Task<int> ExecuteAsync(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(MarkerName);

		var marker = await MarkerRepository.GetOrCreateAsync(connection, MarkerName);

		var currentVersion = await connection.QuerySingleAsync<long>("SELECT CHANGE_TRACKING_CURRENT_VERSION()");
		int result;

		try
		{
			result = await OnExecuteAsync(connection, marker.Version);
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

		return result;
	}

	/// <summary>
	/// represents a specific rollup table in your application. Create instances of this
	/// in your own Rollup instances -- one for each target table
	/// </summary>
	protected abstract class Table<TRollup, TKey> where TKey : notnull
	{
		/// <summary>
		/// this should query a CHANGETABLE(changes {tableName}) table function, accepting a @sinceVersion parameter,
		/// and perform whatever grouping is necessary to derive TRollup
		/// </summary>
		protected abstract Task<IEnumerable<TRollup>> QueryChangesAsync(IDbConnection connection, long sinceVersion);

		/// <summary>
		/// for successful merging, we need to know TEntity's key, 
		/// a single property or combination of properties
		/// </summary>
		protected abstract TKey GetKey(TRollup entity);

		/// <summary>
		/// returns the name of the physical table into which rollup data is merged
		/// </summary>
		protected abstract string TargetTableName { get; }

		/// <summary>
		/// name of the table that is used with the CHANGETABLE function
		/// </summary>
		protected abstract string SourceTableName { get; }

		public async Task<bool> HasChangesAsync(IDbConnection connection, long sinceVersion) =>
			(await connection.QueryAsync<int>(
				$"SELECT 1 FROM CHANGETABLE(changes {SourceTableName}, @sinceVersion) AS [c]",
				new { sinceVersion })).Any();

		public async Task<int> MergeAsync(IDbConnection connection, long sinceVersion)
		{
			var changes = await QueryChangesAsync(connection, sinceVersion);
			await MergeAsync(connection, changes);
			return changes.Count();
		}

		/// <summary>
		/// if you need to apply rollup changes manually, use this
		/// </summary>
		public async Task MergeAsync(IDbConnection connection, IEnumerable<TRollup> changes)
		{
			await connection.DeleteManyAsync(TargetTableName, changes.Select(GetKey));
			await connection.InsertManyAsync(TargetTableName, changes);
		}
	}
}