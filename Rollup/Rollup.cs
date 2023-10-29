﻿using Dapper;
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

	/// <summary>
	/// call your Table.MergeAsync methods here
	/// </summary>
	protected abstract Task OnExecuteAsync(IDbConnection connection, long sinceVersion);

	public async Task ExecuteAsync(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(MarkerName);

		var marker = await MarkerRepository.GetOrCreateAsync(connection, MarkerName);

		var currentVersion = await connection.QuerySingleAsync<long>("SELECT CHANGE_TRACKING_CURRENT_VERSION()");

		try
		{
			await OnExecuteAsync(connection, marker.Version);
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
	protected abstract class Table<TRollup, TKey> where TKey : notnull
	{
		/// <summary>
		/// this should query a CHANGETABLE(changes {tableName}) table function, accepting a @sinceVersion parameter,
		/// and perform whatever grouping is necessary to derive TKey
		/// </summary>
		protected abstract Task<IEnumerable<TKey>> QueryKeyChangesAsync(IDbConnection connection, long sinceVersion);

		/// <summary>
		/// this should perform aggregation of your fact tables for a set of given modified keys.
		/// Your implementation should use QueryWithArrayJoinAsync and a %json% token to indicate where a json array is inserted
		/// within the query. See the integration test for example
		/// </summary>
		protected abstract Task<IEnumerable<TRollup>> QueryRollupRowsAsync(IDbConnection connection, IEnumerable<TKey> keyChanges);

		/// <summary>
		/// for successful merging, we need to know TEntity's key, 
		/// a single property or combination of properties
		/// </summary>
		protected abstract TKey GetKey(TRollup entity);

		/// <summary>
		/// returns the name of the physical table
		/// </summary>
		protected abstract string TableName { get; }

		public async Task MergeAsync(IDbConnection connection, long sinceVersion)
		{
			var keyChanges = await QueryKeyChangesAsync(connection, sinceVersion);
			await connection.DeleteManyAsync(TableName, keyChanges);

			var changes = await QueryRollupRowsAsync(connection, keyChanges);
			await connection.InsertManyAsync(TableName, changes);
		}
	}
}