using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks;

public static class InstrumentListingHistoryWriter
{
    public static bool HasListingChanged(Instrument instrument, NormalizedInstrumentInput input) =>
        !string.Equals(instrument.Ticker, input.Ticker, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(instrument.Figi, input.Figi, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(instrument.CurrencyId, input.CurrencyId, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(instrument.Exchange, input.Exchange, StringComparison.OrdinalIgnoreCase);

    public static InstrumentListingHistory CreateCurrent(Instrument instrument, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Ticker = instrument.Ticker,
            Figi = instrument.Figi,
            CurrencyId = instrument.CurrencyId,
            Exchange = instrument.Exchange,
            EffectiveFrom = now.Date,
            CreatedAt = now,
            UpdatedAt = now
        };

    public static async Task UpsertCurrentAsync(
        LarchikContext context,
        Instrument instrument,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var effectiveFrom = now.Date;
        var activeListing = await context.InstrumentListingHistories
            .AsTracking()
            .Where(x => x.InstrumentId == instrument.Id && (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveFrom))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeListing is null)
        {
            await context.InstrumentListingHistories.AddAsync(CreateCurrent(instrument, now), cancellationToken);
            return;
        }

        if (activeListing.EffectiveFrom.Date >= effectiveFrom)
        {
            ApplyCurrent(activeListing, instrument, now);
            return;
        }

        activeListing.EffectiveTo = effectiveFrom.AddDays(-1);
        activeListing.UpdatedAt = now;

        await context.InstrumentListingHistories.AddAsync(CreateCurrent(instrument, now), cancellationToken);
    }

    private static void ApplyCurrent(InstrumentListingHistory listing, Instrument instrument, DateTime now)
    {
        listing.Ticker = instrument.Ticker;
        listing.Figi = instrument.Figi;
        listing.CurrencyId = instrument.CurrencyId;
        listing.Exchange = instrument.Exchange;
        listing.UpdatedAt = now;
    }
}
