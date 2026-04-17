using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Operations;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.DeleteOperation;

public class DeleteOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
    : IRequestHandler<DeleteOperationCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(DeleteOperationCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var op = await context.Operations
            .Include(o => o.Portfolio)
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.Portfolio != null && o.Portfolio.UserId == userId, cancellationToken);

        if (op is null) return Result<Unit>.Failure("Not found");
        if (OperationTypeRules.IsAdministrativeCorporateAction(op.Type))
        {
            return Result<Unit>.Failure("Split and reverse split must be managed as administrative corporate actions.");
        }

        context.Operations.Remove(op);
        await context.SaveChangesAsync(cancellationToken);

        await recalc.ScheduleRebuild(op.PortfolioId, op.TradeDate, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
