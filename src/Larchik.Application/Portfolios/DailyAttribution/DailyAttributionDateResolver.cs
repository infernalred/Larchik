using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.DailyAttribution;

public static class DailyAttributionDateResolver
{
    public static DailyAttributionPeriod Resolve(
        HistoricalDataLookup data,
        IReadOnlyCollection<Operation> operations,
        IReadOnlyCollection<Guid> instrumentIds,
        IReadOnlyCollection<string> currencies,
        string baseCurrency,
        DateTime requestedDate)
    {
        var cutoff = requestedDate.Date;
        var dates = data.GetMarketDataDates(instrumentIds, currencies, baseCurrency, cutoff)
            .Concat(operations.SelectMany(x => new[]
            {
                x.TradeDate.Date,
                BrokerCashLedgerHelper.GetCashEffectiveDate(x)
            }))
            .Where(x => x <= cutoff)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        if (dates.Length == 0)
        {
            return new DailyAttributionPeriod(cutoff.AddDays(-1), cutoff);
        }

        var valuationDate = dates[^1];
        var comparisonDate = dates.Length > 1 ? dates[^2] : valuationDate.AddDays(-1);
        return new DailyAttributionPeriod(comparisonDate, valuationDate);
    }
}

public sealed record DailyAttributionPeriod(DateTime ComparisonDate, DateTime ValuationDate);
