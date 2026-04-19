using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Helpers;

public static class InstrumentListingHistoryResolver
{
    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> EmptyHistories =
        new Dictionary<Guid, IReadOnlyList<InstrumentListingHistory>>();

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
            return EmptyHistories;
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
        var activeListing = TryResolveActiveListing(instrumentId, histories, asOfDate);
        return activeListing is null
            ? new InstrumentListingSnapshot(ticker, figi, exchange, currencyId)
            : new InstrumentListingSnapshot(
                activeListing.Ticker,
                activeListing.Figi,
                activeListing.Exchange,
                activeListing.CurrencyId);
    }

    public static string ResolveCurrency(
        Instrument instrument,
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> histories,
        DateTime asOfDate) =>
        Resolve(instrument, histories, asOfDate).CurrencyId;

    private static InstrumentListingHistory? TryResolveActiveListing(
        Guid instrumentId,
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> histories,
        DateTime asOfDate)
    {
        if (!histories.TryGetValue(instrumentId, out var instrumentHistory))
        {
            return null;
        }

        var asOfDateOnly = asOfDate.Date;
        return instrumentHistory.FirstOrDefault(x =>
            x.EffectiveFrom.Date <= asOfDateOnly &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= asOfDateOnly));
    }

    public sealed record InstrumentListingSnapshot(
        string Ticker,
        string? Figi,
        string? Exchange,
        string CurrencyId);
}
