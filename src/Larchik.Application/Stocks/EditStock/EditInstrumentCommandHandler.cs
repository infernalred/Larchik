using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.EditStock;

public class EditInstrumentCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<EditInstrumentCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditInstrumentCommand request, CancellationToken cancellationToken)
    {
        var instrument = await context.Instruments
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (instrument is null) return null;

        var listingChanged =
            !string.Equals(instrument.Ticker, request.Model.Ticker, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(instrument.Figi, request.Model.Figi, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(instrument.CurrencyId, request.Model.CurrencyId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(instrument.Exchange, request.Model.Exchange, StringComparison.OrdinalIgnoreCase);

        request.Model.Adapt(instrument);

        var now = DateTime.UtcNow;
        instrument.UpdatedBy = userAccessor.GetUserId();
        instrument.UpdatedAt = now;

        if (listingChanged)
        {
            await UpsertListingHistory(instrument, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }

    private async Task UpsertListingHistory(Instrument instrument, DateTime now, CancellationToken cancellationToken)
    {
        var effectiveFrom = now.Date;
        var activeListing = await context.InstrumentListingHistories
            .Where(x => x.InstrumentId == instrument.Id && (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveFrom))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeListing is null)
        {
            await context.InstrumentListingHistories.AddAsync(new InstrumentListingHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrument.Id,
                Ticker = instrument.Ticker,
                Figi = instrument.Figi,
                CurrencyId = instrument.CurrencyId,
                Exchange = instrument.Exchange,
                EffectiveFrom = effectiveFrom,
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);
            return;
        }

        if (activeListing.EffectiveFrom.Date >= effectiveFrom)
        {
            activeListing.Ticker = instrument.Ticker;
            activeListing.Figi = instrument.Figi;
            activeListing.CurrencyId = instrument.CurrencyId;
            activeListing.Exchange = instrument.Exchange;
            activeListing.UpdatedAt = now;
            return;
        }

        activeListing.EffectiveTo = effectiveFrom.AddDays(-1);
        activeListing.UpdatedAt = now;

        await context.InstrumentListingHistories.AddAsync(new InstrumentListingHistory
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Ticker = instrument.Ticker,
            Figi = instrument.Figi,
            CurrencyId = instrument.CurrencyId,
            Exchange = instrument.Exchange,
            EffectiveFrom = effectiveFrom,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
    }
}
