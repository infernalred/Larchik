using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.CreateOperation;

public class CreateOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
    : IRequestHandler<CreateOperationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOperationCommand request, CancellationToken cancellationToken)
    {
        if (OperationTypeRules.IsAdministrativeCorporateAction(request.Model.Type))
        {
            return Result<Guid>.Failure("Split and reverse split must be managed as administrative corporate actions.");
        }

        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .Where(x => x.Id == request.PortfolioId && x.UserId == userId)
            .Select(x => new PortfolioIdentity(x.Id, x.Broker == null ? null : x.Broker.Code))
            .FirstOrDefaultAsync(cancellationToken);

        if (portfolio is null) return Result<Guid>.Failure("Portfolio not found");

        var resolvedInputResult = await OperationWriteHelper.ResolveInputAsync(context, request.Model, cancellationToken);
        if (!resolvedInputResult.IsSuccess)
        {
            return Result<Guid>.Failure(resolvedInputResult.Error!);
        }

        var resolvedInput = resolvedInputResult.Value!;
        var now = DateTime.UtcNow;

        var entity = new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = request.PortfolioId,
            CreatedAt = now,
            UpdatedAt = now
        };
        OperationWriteHelper.Apply(entity, request.Model, resolvedInput, now);

        entity.BrokerOperationKey = await BrokerOperationIdentityHelper.BuildProvisionalManualKeyAsync(
            context,
            request.PortfolioId,
            portfolio.BrokerCode,
            entity,
            resolvedInput.CanonicalInstrumentCode,
            excludeOperationId: null,
            cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.Operations.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            await recalc.ScheduleRebuild(request.PortfolioId, entity.TradeDate, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (OperationWriteHelper.IsBrokerOperationKeyConflict(ex))
        {
            return Result<Guid>.Failure("Operation with the same broker identity already exists. Please retry the request.");
        }

        return Result<Guid>.Success(entity.Id);
    }

    private sealed record PortfolioIdentity(Guid Id, string? BrokerCode);
}
