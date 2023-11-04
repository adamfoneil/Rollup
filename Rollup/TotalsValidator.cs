using System.Data;

namespace RollupLibrary;

/// <summary>
/// lets you define validation for rollup data
/// </summary>
public abstract class TotalsValidator<TRollup, TKey, TFact> 
	where TKey : notnull 
	where TFact : notnull
{
	protected abstract TKey GetKey(TRollup rollup);
	protected abstract TFact GetFact(TRollup rollup);

	public async Task<(bool AreSame, IEnumerable<(TKey Key, TFact RollupFact, TFact LiveFact)> Mismatches)> CompareAsync(IDbConnection connection)
	{
		var rollup = (await QueryRollupAsync(connection)).ToDictionary(GetKey, GetFact);
		var live = (await QueryLiveAsync(connection)).ToDictionary(GetKey, GetFact);

		var mismatches = rollup.Join(live, rollup => rollup.Key, live => live.Key, (rollupKeyPair, liveKeyPair) => new
		{
			rollupKeyPair.Key,
			RollupFact = rollupKeyPair.Value,
			LiveFact = liveKeyPair.Value
		}).Where(pair => !pair.RollupFact.Equals(pair.LiveFact));

		return (!mismatches.Any(), mismatches.Select(item => (item.Key, item.RollupFact, item.LiveFact)));
	}

	protected abstract Task<IEnumerable<TRollup>> QueryRollupAsync(IDbConnection connection);

	protected abstract Task<IEnumerable<TRollup>> QueryLiveAsync(IDbConnection connection);	
}
