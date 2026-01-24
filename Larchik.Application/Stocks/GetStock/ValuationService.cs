namespace Larchik.Application.Stocks.GetStock;

public class ValuationService
{
    public ValuationResult Evaluate(IEnumerable<ValuationOperation> operations, string? method)
    {
        IValuationStrategy strategy = method?.ToLowerInvariant() switch
        {
            "fifo" => new FifoValuationStrategy(),
            "lifo" => new LifoValuationStrategy(),
            "staticavg" or "staticaverage" => new StaticAverageValuationStrategy(),
            _ => new AdjustingAverageValuationStrategy()
        };

        return strategy.Evaluate(operations);
    }
}
