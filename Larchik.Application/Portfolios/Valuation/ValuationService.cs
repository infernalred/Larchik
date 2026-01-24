namespace Larchik.Application.Portfolios.Valuation;

public class ValuationService
{
    public ValuationResult Evaluate(IEnumerable<ValuationOperation> operations, string? method, bool assumeSorted = false)
    {
        var ordered = assumeSorted
            ? operations
            : operations.OrderBy(o => o.TradeDate).ThenBy(o => o.CreatedAt);

        IValuationStrategy strategy = method?.ToLowerInvariant() switch
        {
            "fifo" => new FifoValuationStrategy(),
            "lifo" => new LifoValuationStrategy(),
            "staticavg" or "staticaverage" => new StaticAverageValuationStrategy(),
            _ => new AdjustingAverageValuationStrategy()
        };

        return strategy.Evaluate(ordered);
    }
}
