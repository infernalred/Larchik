using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetInstrument;

public class GetInstrumentQueryHandler(LarchikContext context)
{
    public async Task<Result<InstrumentDto?>> Handle(GetInstrumentQuery request, CancellationToken cancellationToken)
    {
        var instrument = await context.Instruments
            .Where(x => x.Id == request.Id)
            .Select(InstrumentQueryHelper.AdminDtoProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<InstrumentDto?>.Success(instrument);
    }
}
