namespace Larchik.Application.Portfolios.Valuation;

public sealed class FifoValuationStrategy : LotValuationStrategyBase
{
    protected override int GetConsumptionIndex(int lotCount) => 0;

    protected override IEnumerable<int> GetTraversalIndices(int lotCount) => Enumerable.Range(0, lotCount);
}
