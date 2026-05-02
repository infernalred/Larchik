using Larchik.Application.Helpers;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios;

public static class PortfolioAnalyticsQueryHelper
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

    public static Task<PortfolioAnalyticsContext> LoadAsync(
        LarchikContext context,
        IReadOnlyList<Operation> operations,
        string baseCurrency,
        DateTime maxPriceDate,
        IEnumerable<string>? additionalCurrencies,
        CancellationToken cancellationToken) =>
        LoadAsync(
            context,
            operations,
            baseCurrency,
            maxPriceDate,
            additionalCurrencies,
            cancellationToken,
            useNarrowPriceHistory: true);

    /// <param name="useNarrowPriceHistory">
    /// When true, loads only the latest price row per instrument (as of <paramref name="maxPriceDate"/>), sufficient for MTM at a single as-of.
    /// Set false when <see cref="PortfolioAnalyticsCalculator.CalculatePerformance"/> needs month-end prices across history.
    /// </param>
    public static async Task<PortfolioAnalyticsContext> LoadAsync(
        LarchikContext context,
        IReadOnlyList<Operation> operations,
        string baseCurrency,
        DateTime maxPriceDate,
        IEnumerable<string>? additionalCurrencies,
        CancellationToken cancellationToken,
        bool useNarrowPriceHistory)
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

        List<Price> prices;
        if (!useNarrowPriceHistory)
        {
            prices = await context.Prices
                .Where(x => instrumentIds.Contains(x.InstrumentId) && x.Date <= maxPriceDate)
                .ToListAsync(cancellationToken);
        }
        else if (instrumentIds.Length == 0)
        {
            prices = [];
        }
        else
        {
            prices = await LoadLatestPricesPerInstrumentAsync(
                context,
                instrumentIds,
                maxPriceDate,
                cancellationToken);
        }

        var neededCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseCurrency };
        foreach (var op in mergedOperations)
        {
            neededCurrencies.Add(op.CurrencyId);
        }

        foreach (var instrument in instruments.Values)
        {
            neededCurrencies.Add(instrument.CurrencyId);
        }

        if (additionalCurrencies is not null)
        {
            foreach (var currency in additionalCurrencies.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                neededCurrencies.Add(currency.Trim().ToUpperInvariant());
            }
        }

        var minTradeDate = mergedOperations.Count == 0
            ? (DateTime?)null
            : mergedOperations.Min(o => o.TradeDate).Date;

        var fxRates = await MarketFxRateLoader.LoadAsync(
            context,
            neededCurrencies,
            cancellationToken,
            minRateDate: minTradeDate,
            maxRateDate: maxPriceDate.Date);

        var data = new HistoricalDataLookup(prices, fxRates);

        return new PortfolioAnalyticsContext(mergedOperations, instruments, data);
    }

    /// <summary>
    /// Loads instruments, corporate actions, and a shared <see cref="HistoricalDataLookup"/> for a set of operations (e.g. all portfolios).
    /// Use with per-portfolio <see cref="InstrumentCorporateActionOperationMerger.Merge"/> — merge must stay portfolio-scoped.
    /// </summary>
    public static async Task<PortfolioSharedAnalyticsPools> LoadSharedPoolsAsync(
        LarchikContext context,
        IReadOnlyList<Operation> allOperations,
        string baseCurrency,
        DateTime maxPriceDate,
        IEnumerable<string>? additionalCurrencies,
        CancellationToken cancellationToken,
        bool useNarrowPriceHistory = true)
    {
        var instrumentIds = allOperations
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArray();

        var instruments = await context.Instruments
            .Include(x => x.Category)
            .Where(x => instrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var corporateActions = await InstrumentCorporateActionOperationMerger.LoadAsync(context, instrumentIds, cancellationToken);

        List<Price> prices;
        if (!useNarrowPriceHistory)
        {
            prices = await context.Prices
                .Where(x => instrumentIds.Contains(x.InstrumentId) && x.Date <= maxPriceDate)
                .ToListAsync(cancellationToken);
        }
        else if (instrumentIds.Length == 0)
        {
            prices = [];
        }
        else
        {
            prices = await LoadLatestPricesPerInstrumentAsync(
                context,
                instrumentIds,
                maxPriceDate,
                cancellationToken);
        }

        var neededCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseCurrency };
        foreach (var op in allOperations)
        {
            neededCurrencies.Add(op.CurrencyId);
        }

        foreach (var instrument in instruments.Values)
        {
            neededCurrencies.Add(instrument.CurrencyId);
        }

        if (additionalCurrencies is not null)
        {
            foreach (var currency in additionalCurrencies.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                neededCurrencies.Add(currency.Trim().ToUpperInvariant());
            }
        }

        var minTradeDate = allOperations.Count == 0
            ? (DateTime?)null
            : allOperations.Min(o => o.TradeDate).Date;

        var fxRates = await MarketFxRateLoader.LoadAsync(
            context,
            neededCurrencies,
            cancellationToken,
            minRateDate: minTradeDate,
            maxRateDate: maxPriceDate.Date);

        var data = new HistoricalDataLookup(prices, fxRates);
        return new PortfolioSharedAnalyticsPools(instruments, corporateActions, data);
    }

    /// <summary>
    /// Loads price rows for the latest stored date (≤ maxPriceDate) per instrument without scanning full history.
    /// </summary>
    internal static async Task<List<Price>> LoadLatestPricesPerInstrumentAsync(
        LarchikContext context,
        Guid[] instrumentIds,
        DateTime maxPriceDate,
        CancellationToken cancellationToken)
    {
        if (instrumentIds.Length == 0)
        {
            return [];
        }

        var cutoff = maxPriceDate;
        var maxDatesQuery = context.Prices
            .Where(p => instrumentIds.Contains(p.InstrumentId) && p.Date <= cutoff)
            .GroupBy(p => p.InstrumentId)
            .Select(g => new { InstrumentId = g.Key, MaxDate = g.Max(x => x.Date) });

        return await (
            from p in context.Prices
            join m in maxDatesQuery on new { p.InstrumentId, p.Date } equals new { m.InstrumentId, Date = m.MaxDate }
            select p).ToListAsync(cancellationToken);
    }

    public static DateTime NormalizeMaxPriceDateUtc(DateTime? to)
    {
        var date = to?.Date ?? DateTime.UtcNow.Date;
        var utcDate = date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime().Date,
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };

        return DateTime.SpecifyKind(utcDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
    }

    public sealed record PortfolioAnalyticsContext(
        IReadOnlyList<Operation> Operations,
        IReadOnlyDictionary<Guid, Instrument> Instruments,
        HistoricalDataLookup Data);

    public sealed record PortfolioSharedAnalyticsPools(
        IReadOnlyDictionary<Guid, Instrument> Instruments,
        IReadOnlyList<InstrumentCorporateAction> CorporateActions,
        HistoricalDataLookup Data);
}
