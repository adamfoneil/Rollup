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

	protected abstract bool FactsAreEqual(TResult transactional, TResult rollup);

	protected abstract Task<IEnumerable<TResult>> QueryTransactionalAsync(IDbConnection connection);

	protected abstract Task<IEnumerable<TResult>> QueryRollupAsync(IDbConnection connection);

	public async Task<Result> QueryAsync(IDbConnection connection)
	{
		List<(MismatchType, TResult)> dimensions = new();

		var transactionalDetail = (await QueryTransactionalAsync(connection)).ToDictionary(GetIdentity);
		var rollupDetail = (await QueryRollupAsync(connection)).ToDictionary(GetIdentity);

		dimensions.AddRange(transactionalDetail.Keys.Except(rollupDetail.Keys).Select(id => (MismatchType.NotInRollup, transactionalDetail[id])));
		dimensions.AddRange(rollupDetail.Keys.Except(transactionalDetail.Keys).Select(id => (MismatchType.NotInTransactional, rollupDetail[id])));

		var facts = transactionalDetail.Keys.Join(rollupDetail.Keys, id => id, id => id, (leftId, rightId) => new
		{
			TransactionValue = transactionalDetail[leftId],
			RollupValue = rollupDetail[rightId]
		}).Where(pair => !FactsAreEqual(pair.TransactionValue, pair.RollupValue))
		.Select(pair => (pair.TransactionValue, pair.RollupValue))
		.ToArray();

		return new Result()
		{
			Dimensions = dimensions,
			Facts = facts
		};
	}

	public class Result
	{
		public required IEnumerable<(MismatchType Type, TResult Data)> Dimensions { get; init; } = Enumerable.Empty<(MismatchType, TResult)>();
		public required IEnumerable<(TResult TransactionalRow, TResult RollupRow)> Facts { get; init; } = Enumerable.Empty<(TResult, TResult)>();
	}
}
