using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios;

/// <summary>
/// Cheap aggregates over market data so portfolio summary memory cache invalidates when prices/FX change without a matching operation edit.
/// </summary>
public static class PortfolioSummaryMarketDataFingerprint
{
    public static async Task<(long MaxPriceDataTicks, long MaxFxRateDataTicks)> ForPortfolioAsync(
        LarchikContext context,
        Guid portfolioId,
        string reportingCurrencyId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var opQuery = context.Operations.Where(x => x.PortfolioId == portfolioId && x.TradeDate <= asOfUtc);
        return await ComputeAsync(context, opQuery, reportingCurrencyId, asOfUtc, cancellationToken);
    }

    public static async Task<(long MaxPriceDataTicks, long MaxFxRateDataTicks)> ForPortfoliosAsync(
        LarchikContext context,
        IReadOnlyCollection<Guid> portfolioIds,
        string baseCurrency,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        if (portfolioIds.Count == 0)
        {
            return (0L, 0L);
        }

        var opQuery = context.Operations.Where(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfUtc);
        return await ComputeAsync(context, opQuery, baseCurrency, asOfUtc, cancellationToken);
    }

    private static async Task<(long MaxPriceDataTicks, long MaxFxRateDataTicks)> ComputeAsync(
        LarchikContext context,
        IQueryable<Operation> opQuery,
        string reportingCurrencyId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var instrumentIds = await opQuery
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var currencyIds = await opQuery
            .Select(x => x.CurrencyId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var currencies = new HashSet<string>(currencyIds, StringComparer.OrdinalIgnoreCase)
        {
            reportingCurrencyId.Trim().ToUpperInvariant()
        };

        if (instrumentIds.Length > 0)
        {
            var quoteCurrencies = await context.Instruments
                .Where(i => instrumentIds.Contains(i.Id))
                .Select(i => i.CurrencyId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
            foreach (var c in quoteCurrencies)
            {
                if (!string.IsNullOrWhiteSpace(c))
                {
                    currencies.Add(c.Trim().ToUpperInvariant());
                }
            }
        }

        var marketFxIds = await MarketFxRateLoader.GetMarketFxInstrumentIdsAsync(context, currencies, cancellationToken);
        var allPriceInstrumentIds = instrumentIds.Concat(marketFxIds).Distinct().ToArray();

        long maxPriceTicks = 0;
        if (allPriceInstrumentIds.Length > 0)
        {
            var priceQuery = context.Prices.Where(p =>
                allPriceInstrumentIds.Contains(p.InstrumentId) &&
                p.Date <= asOfUtc);
            if (await priceQuery.AnyAsync(cancellationToken))
            {
                var maxU = await priceQuery.MaxAsync(p => p.UpdatedAt, cancellationToken);
                maxPriceTicks = maxU.Ticks;
            }
        }

        long maxFxTicks = 0;
        var currArr = currencies.ToArray();
        if (currArr.Length >= 2)
        {
            var fxQuery = context.FxRates.Where(x =>
                currArr.Contains(x.BaseCurrencyId) &&
                currArr.Contains(x.QuoteCurrencyId) &&
                x.Date <= asOfUtc);
            if (await fxQuery.AnyAsync(cancellationToken))
            {
                var maxU = await fxQuery.MaxAsync(x => x.UpdatedAt, cancellationToken);
                maxFxTicks = maxU.Ticks;
            }
        }

        return (maxPriceTicks, maxFxTicks);
    }
}
