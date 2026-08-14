namespace Larchik.Application.Portfolios.DailyAttribution;

public static class DailyAttributionCacheKeys
{
    public static string Single(
        Guid userId,
        Guid portfolioId,
        DateTime requestedDate,
        int operationCount,
        long maxOperationCreatedTicks,
        long maxOperationUpdatedTicks,
        long maxPriceDataTicks,
        long maxFxRateDataTicks) =>
        $"daily-attribution:{userId}:{portfolioId}:{requestedDate.Date.Ticks}:{operationCount}:{maxOperationCreatedTicks}:{maxOperationUpdatedTicks}:{maxPriceDataTicks}:{maxFxRateDataTicks}";

    public static string Aggregate(
        Guid userId,
        IReadOnlyCollection<Guid> portfolioIds,
        string baseCurrency,
        DateTime requestedDate,
        int operationCount,
        long maxOperationCreatedTicks,
        long maxOperationUpdatedTicks,
        long maxPriceDataTicks,
        long maxFxRateDataTicks)
    {
        var ids = string.Join(',', portfolioIds.OrderBy(x => x));
        return $"aggregate-daily-attribution:{userId}:{ids}:{baseCurrency}:{requestedDate.Date.Ticks}:{operationCount}:{maxOperationCreatedTicks}:{maxOperationUpdatedTicks}:{maxPriceDataTicks}:{maxFxRateDataTicks}";
    }
}
