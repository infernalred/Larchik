using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetInstrument;

public class GetInstrumentQueryHandler(LarchikContext context) : IRequestHandler<GetInstrumentQuery, Result<InstrumentDto?>>
{
    public async Task<Result<InstrumentDto?>> Handle(GetInstrumentQuery request, CancellationToken cancellationToken)
    {
        var instrument = await context.Instruments
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(InstrumentQueryHelper.AdminDtoProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<InstrumentDto?>.Success(instrument);
    }
}
