using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.DeleteOperation;

public class DeleteOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
{
    private const string OperationNotFoundMessage = "Not found";
    private const string AdministrativeOperationMessage = "Split and reverse split must be managed as administrative corporate actions.";
    private const string AlreadyDeletedMessage = "Operation was already deleted.";
    private const string DeleteFailedMessage = "Failed to delete operation due to database constraints.";
    private const string RebuildFailedMessage = "Operation delete rolled back because portfolio rebuild scheduling failed.";
    private const string CommitFailedMessage = "Failed to commit operation deletion to database.";

    public async Task<Result<Unit>> Handle(DeleteOperationCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var op = await context.Operations
            .AsTracking()
            .Include(o => o.Portfolio)
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.Portfolio != null && o.Portfolio.UserId == userId, cancellationToken);

        if (op is null) return Result<Unit>.Failure(OperationNotFoundMessage);
        if (OperationTypeRules.IsAdministrativeCorporateAction(op.Type))
        {
            return Result<Unit>.Failure(AdministrativeOperationMessage);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.Operations.Remove(op);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Unit>.Failure(AlreadyDeletedMessage);
        }
        catch (DbUpdateException)
        {
            return Result<Unit>.Failure(DeleteFailedMessage);
        }

        try
        {
            await recalc.ScheduleRebuild(op.PortfolioId, op.TradeDate, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // ScheduleRebuild is executed after SaveChanges but before CommitAsync,
            // so on failure we rely on transaction disposal to rollback the delete.
            return Result<Unit>.Failure(RebuildFailedMessage);
        }

        try
        {
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Unit>.Failure(OperationNotFoundMessage);
        }
        catch (DbUpdateException)
        {
            return Result<Unit>.Failure(CommitFailedMessage);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<Unit>.Failure(CommitFailedMessage);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
