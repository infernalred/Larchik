using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.EditOperation;

public class EditOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
    : IRequestHandler<EditOperationCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditOperationCommand request, CancellationToken cancellationToken)
    {
        if (OperationTypeRules.IsAdministrativeCorporateAction(request.Model.Type))
        {
            return Result<Unit>.Failure("Split and reverse split must be managed as administrative corporate actions.");
        }

        var userId = userAccessor.GetUserId();
        var op = await context.Operations
            .AsTracking()
            .Include(o => o.Portfolio)
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.Portfolio != null && o.Portfolio.UserId == userId, cancellationToken);

        if (op is null) return null;
        if (OperationTypeRules.IsAdministrativeCorporateAction(op.Type))
        {
            return Result<Unit>.Failure("Split and reverse split must be managed as administrative corporate actions.");
        }

        var originalTradeDate = op.TradeDate;
        var resolvedInputResult = await OperationWriteHelper.ResolveInputAsync(context, request.Model, cancellationToken);
        if (!resolvedInputResult.IsSuccess)
        {
            return Result<Unit>.Failure(resolvedInputResult.Error!);
        }

        var resolvedInput = resolvedInputResult.Value!;
        var brokerCode = await context.Portfolios
            .AsNoTracking()
            .Where(x => x.Id == op.PortfolioId)
            .Select(x => x.Broker == null ? null : x.Broker.Code)
            .FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;

        OperationWriteHelper.Apply(op, request.Model, resolvedInput, now);
        if (!BrokerOperationIdentityHelper.IsConfirmedImportedKey(op.BrokerOperationKey))
        {
            op.BrokerOperationKey = await BrokerOperationIdentityHelper.BuildProvisionalManualKeyAsync(
                context,
                op.PortfolioId,
                brokerCode,
                op,
                resolvedInput.CanonicalInstrumentCode,
                op.Id,
                cancellationToken);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);

            var fromDate = originalTradeDate < op.TradeDate ? originalTradeDate : op.TradeDate;
            await recalc.ScheduleRebuild(op.PortfolioId, fromDate, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (OperationWriteHelper.IsBrokerOperationKeyConflict(ex))
        {
            return Result<Unit>.Failure("Operation with the same broker identity already exists. Please retry the request.");
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
