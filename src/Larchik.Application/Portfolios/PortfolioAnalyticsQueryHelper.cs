using Larchik.Application.Helpers;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios;

internal static class PortfolioAnalyticsQueryHelper
{
    public static string? ResolveBaseCurrency(string? requestedCurrency, IReadOnlyCollection<Portfolio> portfolios)
    {
        if (!string.IsNullOrWhiteSpace(requestedCurrency))
        {
            return requestedCurrency.Trim().ToUpperInvariant();
        }

        var distinct = portfolios
            .Select(x => x.ReportingCurrencyId.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinct.Length == 1 ? distinct[0] : null;
    }

    public static async Task<PortfolioAnalyticsContext> LoadAsync(
        LarchikContext context,
        IReadOnlyList<Operation> operations,
        string baseCurrency,
        DateTime maxPriceDate,
        CancellationToken cancellationToken)
    {
        var instrumentIds = operations
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArray();

        var instruments = await context.Instruments
            .Include(x => x.Category)
            .Where(x => instrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var corporateActions = await InstrumentCorporateActionOperationMerger.LoadAsync(context, instrumentIds, cancellationToken);
        var mergedOperations = InstrumentCorporateActionOperationMerger.Merge(operations, corporateActions, instruments).ToList();

        var prices = await context.Prices
            .Where(x => instrumentIds.Contains(x.InstrumentId) && x.Date <= maxPriceDate)
            .ToListAsync(cancellationToken);

        var neededCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseCurrency };
        foreach (var op in mergedOperations)
        {
            neededCurrencies.Add(op.CurrencyId);
        }

        foreach (var instrument in instruments.Values)
        {
            neededCurrencies.Add(instrument.CurrencyId);
        }

        var fxRates = await MarketFxRateLoader.LoadAsync(context, neededCurrencies, cancellationToken);
        var data = new HistoricalDataLookup(prices, fxRates);

        return new PortfolioAnalyticsContext(mergedOperations, instruments, data);
    }

    internal sealed record PortfolioAnalyticsContext(
        IReadOnlyList<Operation> Operations,
        IReadOnlyDictionary<Guid, Instrument> Instruments,
        HistoricalDataLookup Data);
}
