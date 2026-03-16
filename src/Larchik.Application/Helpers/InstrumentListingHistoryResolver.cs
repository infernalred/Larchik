using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Helpers;

public static class InstrumentListingHistoryResolver
{
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>>> LoadAsync(
        LarchikContext context,
        IEnumerable<Guid> instrumentIds,
        CancellationToken cancellationToken)
    {
        var ids = instrumentIds
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<InstrumentListingHistory>>();
        }

        var rows = await context.InstrumentListingHistories
            .AsNoTracking()
            .Where(x => ids.Contains(x.InstrumentId))
            .OrderBy(x => x.InstrumentId)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<InstrumentListingHistory>)x.ToList());
    }

    public static InstrumentListingSnapshot Resolve(
        Instrument instrument,
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> histories,
        DateTime asOfDate)
    {
        return Resolve(
            instrument.Id,
            instrument.Ticker,
            instrument.Figi,
            instrument.Exchange,
            instrument.CurrencyId,
            histories,
            asOfDate);
    }

    public static InstrumentListingSnapshot Resolve(
        Guid instrumentId,
        string ticker,
        string? figi,
        string? exchange,
        string currencyId,
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> histories,
        DateTime asOfDate)
    {
        if (histories.TryGetValue(instrumentId, out var instrumentHistory))
        {
            var activeListing = instrumentHistory.FirstOrDefault(x =>
                x.EffectiveFrom.Date <= asOfDate.Date &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= asOfDate.Date));

            if (activeListing is not null)
            {
                return new InstrumentListingSnapshot(
                    activeListing.Ticker,
                    activeListing.Figi,
                    activeListing.Exchange,
                    activeListing.CurrencyId);
            }
        }

        return new InstrumentListingSnapshot(ticker, figi, exchange, currencyId);
    }

    public static string ResolveCurrency(
        Instrument instrument,
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> histories,
        DateTime asOfDate)
    {
        return Resolve(instrument, histories, asOfDate).CurrencyId;
    }

    public sealed record InstrumentListingSnapshot(
        string Ticker,
        string? Figi,
        string? Exchange,
        string CurrencyId);
}
