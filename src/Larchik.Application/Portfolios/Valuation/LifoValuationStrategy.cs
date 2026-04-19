namespace Larchik.Application.Portfolios.Valuation;

public sealed class LifoValuationStrategy : LotValuationStrategyBase
{
    protected override int GetConsumptionIndex(int lotCount) => lotCount - 1;

    protected override IEnumerable<int> GetTraversalIndices(int lotCount) =>
        Enumerable.Range(0, lotCount).Reverse();
}
