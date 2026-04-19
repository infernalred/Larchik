using System.Collections.Frozen;

namespace Larchik.Application.Portfolios.Valuation;

public sealed class ValuationService
{
    private static readonly IValuationStrategy DefaultStrategy = new AdjustingAverageValuationStrategy();

    private static readonly FrozenDictionary<string, IValuationStrategy> Strategies =
        new Dictionary<string, IValuationStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            ["fifo"] = new FifoValuationStrategy(),
            ["lifo"] = new LifoValuationStrategy(),
            ["staticavg"] = new StaticAverageValuationStrategy(),
            ["staticaverage"] = new StaticAverageValuationStrategy()
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public ValuationResult Evaluate(IEnumerable<ValuationOperation> operations, string? method, bool assumeSorted = false)
    {
        var ordered = assumeSorted
            ? operations
            : operations.OrderBy(o => o.TradeDate).ThenBy(o => o.CreatedAt);

        var strategy = method is not null && Strategies.TryGetValue(method, out var resolved)
            ? resolved
            : DefaultStrategy;

        return strategy.Evaluate(ordered);
    }
}
