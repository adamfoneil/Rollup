using Dapper;
using Microsoft.Extensions.Logging;
using RollupLibrary.Interfaces;
using System.Data;

namespace RollupLibrary;

/// <summary>
/// encapsulates a change tracking query that lets you merge incremental changes to one or more rollup tables
/// </summary>
/// <typeparam name="TKey">key of your impacted rollup table(s)</typeparam>
public abstract class Rollup<TKey>
{
	private readonly IMarkerRepository MarkerRepository;
	private readonly ILogger<Rollup<TKey>> Logger;

	public Rollup(IMarkerRepository markerRepository, ILogger<Rollup<TKey>> logger)
	{
		MarkerRepository = markerRepository;
		Logger = logger;
	}

	protected string MarkerName { get; } = default!;

	/// <summary>
	/// this should query a CHANGETABLE(changes {tableName}) table function, accepting a @sinceVersion parameter	
	/// </summary>
	protected abstract Task<IEnumerable<TKey>> QueryKeyChangesAsync(IDbConnection connection, long sinceVersion);

	/// <summary>
	/// this should insert/update (i.e. merge) your rollup table(s)
	/// </summary>
	public abstract Task MergeAsync(IDbConnection connection, IEnumerable<TKey> keyChanges);

	public async Task MergeAsync(IDbConnection connection)
	{
		var marker = await MarkerRepository.GetOrCreateAsync(connection, MarkerName);

		var currentVersion = await connection.QuerySingleAsync<long>("SELECT CHANGE_TRACKING_CURRENT_VERSION()");
			
		IEnumerable<TKey> keyChanges;

		try
		{
			Logger.LogInformation("Querying key changes for {rollupType}", GetType().Name);
			keyChanges = await QueryKeyChangesAsync(connection, marker.Version);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error querying key changes for {rollupType}", GetType().Name);
			throw;
		}

		if (!keyChanges.Any())
		{
			Logger.LogInformation("No changes found since version {version}", marker.Version);
			return;
		}
		
		try
		{
			Logger.LogInformation("Merging changes for {rollupType}", GetType().Name);
			await MergeAsync(connection, keyChanges);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error merging changes for {rollupType}", GetType().Name);
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
}