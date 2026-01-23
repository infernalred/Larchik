using Larchik.Persistence.Entities;

namespace Larchik.Application.Valuations;

public class ValuationService
{
    public ValuationResult Evaluate(IEnumerable<Operation> operations, string? method)
    {
        IValuationStrategy strategy = method?.ToLowerInvariant() switch
        {
            "fifo" => new FifoValuationStrategy(),
            "staticavg" or "staticaverage" => new StaticAverageValuationStrategy(),
            _ => new AdjustingAverageValuationStrategy()
        };

        return strategy.Evaluate(operations);
    }
}
