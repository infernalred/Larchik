using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.GetInstrumentCorporateActions;

public class GetInstrumentCorporateActionsQueryHandler(LarchikContext context)
    : IRequestHandler<GetInstrumentCorporateActionsQuery, Result<IReadOnlyCollection<InstrumentCorporateActionDto>>>
{
    public async Task<Result<IReadOnlyCollection<InstrumentCorporateActionDto>>> Handle(
        GetInstrumentCorporateActionsQuery request,
        CancellationToken cancellationToken)
    {
        var exists = await context.Instruments
            .AnyAsync(x => x.Id == request.InstrumentId, cancellationToken);

        if (!exists)
        {
            return Result<IReadOnlyCollection<InstrumentCorporateActionDto>>.Failure("Instrument not found.");
        }

        var items = await context.InstrumentCorporateActions
            .Where(x =>
                x.InstrumentId == request.InstrumentId &&
                InstrumentCorporateActionRules.IsSupportedType(x.Type))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.Type)
            .Select(x => new InstrumentCorporateActionDto
            {
                Id = x.Id,
                InstrumentId = x.InstrumentId,
                Type = x.Type,
                Factor = x.Factor,
                EffectiveDate = x.EffectiveDate,
                Note = x.Note
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyCollection<InstrumentCorporateActionDto>>.Success(items);
    }
}
