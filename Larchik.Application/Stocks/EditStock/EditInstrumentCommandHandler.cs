using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Instruments.EditInstrument;

public class EditInstrumentCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<EditInstrumentCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditInstrumentCommand request, CancellationToken cancellationToken)
    {
        var instrument = await context.Instruments
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (instrument is null) return null;

        request.Model.Adapt(instrument);

        instrument.UpdatedBy = userAccessor.GetUserId();
        instrument.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
