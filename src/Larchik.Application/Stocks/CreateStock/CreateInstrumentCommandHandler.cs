using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Mapster;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public class CreateInstrumentCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<CreateInstrumentCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(CreateInstrumentCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var now = DateTime.UtcNow;

        var instrument = request.Model.Adapt<Instrument>();
        instrument.Id = Guid.NewGuid();
        instrument.CreatedBy = userId;
        instrument.UpdatedBy = userId;
        instrument.CreatedAt = now;
        instrument.UpdatedAt = now;

        await context.Instruments.AddAsync(instrument, cancellationToken);
        await context.InstrumentListingHistories.AddAsync(new InstrumentListingHistory
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
        }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
