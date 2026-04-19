using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public class CreateInstrumentCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<CreateInstrumentCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(CreateInstrumentCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var now = DateTime.UtcNow;
        var input = InstrumentInputNormalizer.Normalize(request.Model);

        var validationError = await InstrumentWriteGuard.ValidateAsync(context, input, excludeInstrumentId: null, cancellationToken);
        if (validationError is not null)
        {
            return Result<Unit>.Failure(validationError);
        }

        var instrument = new Instrument
        {
            Id = Guid.NewGuid()
        };
        InstrumentInputNormalizer.ApplyTo(instrument, input);
        instrument.CreatedBy = userId;
        instrument.UpdatedBy = userId;
        instrument.CreatedAt = now;
        instrument.UpdatedAt = now;

        await context.Instruments.AddAsync(instrument, cancellationToken);
        await context.InstrumentListingHistories.AddAsync(
            InstrumentListingHistoryWriter.CreateCurrent(instrument, now),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
