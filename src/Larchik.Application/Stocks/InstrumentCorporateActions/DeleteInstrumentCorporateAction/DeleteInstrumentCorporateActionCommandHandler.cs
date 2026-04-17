using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Stocks.InstrumentCorporateActions.CreateInstrumentCorporateAction;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.DeleteInstrumentCorporateAction;

public class DeleteInstrumentCorporateActionCommandHandler(LarchikContext context, IPortfolioRecalcService recalc)
    : IRequestHandler<DeleteInstrumentCorporateActionCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(DeleteInstrumentCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.InstrumentCorporateActions
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.InstrumentId == request.InstrumentId, cancellationToken);

        if (entity is null)
        {
            return Result<Unit>.Failure("Not found");
        }

        var rebuildFrom = entity.EffectiveDate;
        context.InstrumentCorporateActions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        await CreateInstrumentCorporateActionCommandHandler.ScheduleAffectedPortfoliosRebuildAsync(
            context,
            recalc,
            request.InstrumentId,
            rebuildFrom,
            cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
