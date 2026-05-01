using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlertsSummary;

public sealed class GetPortfolioReconciliationAlertsSummaryQueryHandler(LarchikContext context, IUserAccessor userAccessor)
{
    public async Task<Result<PortfolioReconciliationAlertsSummaryDto>> Handle(
        GetPortfolioReconciliationAlertsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var userPortfolioIds = await context.Portfolios
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var query = context.PortfolioReconciliationResults
            .Where(x => userPortfolioIds.Contains(x.PortfolioId) && x.AlertRequired);

        if (request.From.HasValue)
        {
            var fromUtc = EnsureUtc(request.From.Value).Date;
            query = query.Where(x => x.StatementDate >= fromUtc);
        }

        if (request.To.HasValue)
        {
            var toUtcInclusive = EnsureUtc(request.To.Value).Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.StatementDate <= toUtcInclusive);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            query = query.Where(x => x.Source == source);
        }

        var totalAlerts = await query.CountAsync(cancellationToken);
        var criticalAlertsCount = await query.CountAsync(x => x.Severity == "critical", cancellationToken);
        var warningAlerts = await query.CountAsync(x => x.Severity == "warning", cancellationToken);

        var criticalAlerts = await query
            .Where(x => x.Severity == "critical")
            .OrderByDescending(x => x.StatementDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestCriticalByPortfolio = criticalAlerts
            .GroupBy(x => x.PortfolioId)
            .Select(x => x.First())
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
            .ToList();

        return Result<PortfolioReconciliationAlertsSummaryDto>.Success(new PortfolioReconciliationAlertsSummaryDto
        {
            TotalAlerts = totalAlerts,
            CriticalAlerts = criticalAlertsCount,
            WarningAlerts = warningAlerts,
            LatestCriticalByPortfolio = latestCriticalByPortfolio
        });
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
