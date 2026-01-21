using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Instruments.GetInstrument;

public class GetInstrumentQueryHandler(LarchikContext context) : IRequestHandler<GetInstrumentQuery, Result<InstrumentDto?>>
{
    public async Task<Result<InstrumentDto?>> Handle(GetInstrumentQuery request, CancellationToken cancellationToken)
    {
        var instrument = await context.Instruments
            .ProjectToType<InstrumentDto>()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        return Result<InstrumentDto?>.Success(instrument);
    }
}
