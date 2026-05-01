using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.Reconciliation.GetLatestPortfolioReconciliationResult;

public sealed class GetLatestPortfolioReconciliationResultQueryHandler(LarchikContext context, IUserAccessor userAccessor)
{
    public async Task<Result<PortfolioReconciliationResultDto>> Handle(
        GetLatestPortfolioReconciliationResultQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var hasPortfolioAccess = await context.Portfolios
            .AnyAsync(x => x.Id == request.PortfolioId && x.UserId == userId, cancellationToken);
        if (!hasPortfolioAccess)
        {
            return Result<PortfolioReconciliationResultDto>.Failure("Portfolio not found");
        }

        var query = context.PortfolioReconciliationResults
            .Where(x => x.PortfolioId == request.PortfolioId);
        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            query = query.Where(x => x.Source == source);
        }

        var item = await query
            .OrderByDescending(x => x.StatementDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new PortfolioReconciliationResultDto
            {
                Id = x.Id,
                PortfolioId = x.PortfolioId,
                StatementDate = x.StatementDate,
                Source = x.Source,
                ReportingCurrencyId = x.ReportingCurrencyId,
                Status = x.Status,
                Severity = x.Severity,
                AlertRequired = x.AlertRequired,
                ReasonCode = x.ReasonCode,
                ToleranceBase = x.ToleranceBase,
                ActualNavBase = x.ActualNavBase,
                ActualCashBase = x.ActualCashBase,
                ActualPositionsValueBase = x.ActualPositionsValueBase,
                TargetNavBase = x.TargetNavBase,
                TargetCashBase = x.TargetCashBase,
                TargetPositionsValueBase = x.TargetPositionsValueBase,
                NavDelta = x.NavDelta,
                CashDelta = x.CashDelta,
                PositionsDelta = x.PositionsDelta,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return item is null
            ? Result<PortfolioReconciliationResultDto>.Failure("Reconciliation result not found")
            : Result<PortfolioReconciliationResultDto>.Success(item);
    }
}
