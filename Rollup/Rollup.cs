using Dapper;
using Microsoft.Extensions.Logging;
using RollupLibrary.Interfaces;
using System.Data;

namespace RollupLibrary;

/// <summary>
/// defines an incrementally-updated table using SQL Server change tracking
/// </summary>
/// <typeparam name="T">rollup table entity</typeparam>
public abstract class Rollup<T>
{
	private readonly IMarkerRepository MarkerRepository;
	private readonly ILogger<Rollup<T>> Logger;

	public Rollup(IMarkerRepository markerRepository, ILogger<Rollup<T>> logger)
	{
		MarkerRepository = markerRepository;
		Logger = logger;
	}

	protected string MarkerName { get; } = default!;

	/// <summary>
	/// this should query a CHANGETABLE(changes {tableName}) table function, accepting a @sinceVersion parameter,
	/// and perform some kind of transform or aggregation in order to derive your T type
	/// </summary>
	protected abstract Task<IEnumerable<T>> QueryChangesAsync(IDbConnection connection, long sinceVersion);

	/// <summary>
	/// this should save your rollup data
	/// </summary>
	public abstract Task StoreChangesAsync(IDbConnection connection, IEnumerable<T> changes);

	public async Task UpdateAsync(IDbConnection connection)
	{
		var marker = await MarkerRepository.GetOrCreateAsync(connection, MarkerName);

		var currentVersion = await connection.QuerySingleAsync<long>("SELECT CHANGE_TRACKING_CURRENT_VERSION()");
			
		IEnumerable<T> changes;

		try
		{
			Logger.LogInformation("Querying changes for {rollupType}", GetType().Name);
			changes = await QueryChangesAsync(connection, marker.Version);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error querying changes for {rollupType}", GetType().Name);
			throw;
		}

		if (!changes.Any())
		{
			Logger.LogInformation("No changes found since version {version}", marker.Version);
			return;
		}
		
		try
		{
			Logger.LogInformation("Storing changes for {rollupType}", GetType().Name);
			await StoreChangesAsync(connection, changes);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error storing changes for {rollupType}", GetType().Name);
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