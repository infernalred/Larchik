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

        var instrument = request.Model.Adapt<Instrument>();
        instrument.Id = Guid.NewGuid();
        instrument.CreatedBy = userId;
        instrument.UpdatedBy = userId;

        await context.Instruments.AddAsync(instrument, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
