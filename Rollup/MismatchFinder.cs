using System.Data;

namespace RollupLibrary;

public enum MismatchType
{
	NotInTransactional,
	NotInRollup
}

/// <summary>
/// defines a way to find mismatched detail rows between two report queries,
/// used for troubleshooting rollups
/// </summary>
public abstract class MismatchFinder<TResult, TIdentity>
	where TIdentity : struct
	where TResult : notnull
{
	protected abstract TIdentity GetIdentity(TResult result);

	protected abstract Task<IEnumerable<TResult>> QueryTransactionalAsync(IDbConnection connection);

	protected abstract Task<IEnumerable<TResult>> QueryRollupAsync(IDbConnection connection);

	public async Task<IEnumerable<(MismatchType Type, TResult Data)>> QueryAsync(IDbConnection connection)
	{
		List<(MismatchType, TResult)> results = new();

		var transactionalDetail = (await QueryTransactionalAsync(connection)).ToDictionary(GetIdentity);
		var rollupDetail = (await QueryRollupAsync(connection)).ToDictionary(GetIdentity);

		results.AddRange(transactionalDetail.Keys.Except(rollupDetail.Keys).Select(id => (MismatchType.NotInRollup, transactionalDetail[id])));
		results.AddRange(rollupDetail.Keys.Except(transactionalDetail.Keys).Select(id => (MismatchType.NotInTransactional, rollupDetail[id])));

		return results;
	}
}
