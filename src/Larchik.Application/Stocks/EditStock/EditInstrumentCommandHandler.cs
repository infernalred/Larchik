using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Stocks;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
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

        var input = InstrumentInputNormalizer.Normalize(request.Model);
        var listingChanged = InstrumentListingHistoryWriter.HasListingChanged(instrument, input);

        var validationError = await InstrumentWriteGuard.ValidateAsync(context, input, instrument.Id, cancellationToken);
        if (validationError is not null)
        {
            return Result<Unit>.Failure(validationError);
        }

        InstrumentInputNormalizer.ApplyTo(instrument, input);

        var now = DateTime.UtcNow;
        instrument.UpdatedBy = userAccessor.GetUserId();
        instrument.UpdatedAt = now;

        if (listingChanged)
        {
            await InstrumentListingHistoryWriter.UpsertCurrentAsync(context, instrument, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
