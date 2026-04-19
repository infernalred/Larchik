using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Stocks.InstrumentCorporateActions;
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
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.InstrumentId == request.InstrumentId, cancellationToken);

        if (entity is null)
        {
            return Result<Unit>.Failure("Not found");
        }

        var rebuildFrom = entity.EffectiveDate;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.InstrumentCorporateActions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        await InstrumentCorporateActionWriteHelper.ScheduleAffectedPortfoliosRebuildAsync(
            context,
            recalc,
            request.InstrumentId,
            rebuildFrom,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
