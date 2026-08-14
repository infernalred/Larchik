using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.DailyAttribution;

public static class DailyAttributionInstrumentSelector
{
    public static IReadOnlyCollection<Guid> SelectHeldMarketInstruments(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime asOfDate)
    {
        var ledger = PortfolioLedgerAccumulator.Accumulate(
            portfolio,
            operations,
            instruments,
            data,
            baseCurrency,
            asOfDate);

        return ledger.Positions
            .Where(x => x.Value != 0m &&
                        instruments.TryGetValue(x.Key, out var instrument) &&
                        instrument.Type != InstrumentType.Currency)
            .Select(x => x.Key)
            .ToArray();
    }
}
