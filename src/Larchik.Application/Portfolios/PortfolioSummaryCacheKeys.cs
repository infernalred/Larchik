namespace Larchik.Application.Portfolios;

public static class PortfolioSummaryCacheKeys
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

    public static string Single(
        Guid userId,
        Guid portfolioId,
        string valuationMethod,
        int operationCount,
        long maxOperationCreatedTicks,
        long maxOperationUpdatedTicks,
        long maxPriceDataTicks,
        long maxFxRateDataTicks) =>
        $"portfolio-summary:{userId}:{portfolioId}:{valuationMethod}:{operationCount}:{maxOperationCreatedTicks}:{maxOperationUpdatedTicks}:{maxPriceDataTicks}:{maxFxRateDataTicks}";

    public static string Aggregate(
        Guid userId,
        string valuationMethod,
        string reportingCurrency,
        IReadOnlyCollection<Guid> portfolioIds,
        int operationCount,
        long maxOperationCreatedTicks,
        long maxOperationUpdatedTicks,
        long maxPriceDataTicks,
        long maxFxRateDataTicks)
    {
        var sorted = string.Join(
            ',',
            portfolioIds.OrderBy(x => x, Comparer<Guid>.Default));
        return $"aggregate-summary:{userId}:{valuationMethod}:{reportingCurrency}:{sorted}:{operationCount}:{maxOperationCreatedTicks}:{maxOperationUpdatedTicks}:{maxPriceDataTicks}:{maxFxRateDataTicks}";
    }
}
