using Larchik.Application.Common.Paging;
using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Reconciliation;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlerts;

public sealed class GetPortfolioReconciliationAlertsQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfolioReconciliationAlertsQuery, Result<PagedResult<PortfolioReconciliationResultDto>>>
{
    private const int MaxPageSize = 200;

    public async Task<Result<PagedResult<PortfolioReconciliationResultDto>>> Handle(
        GetPortfolioReconciliationAlertsQuery request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateSeverity(request.Severity, out var severityError))
        {
            return Result<PagedResult<PortfolioReconciliationResultDto>>.Failure(severityError!);
        }

        var userId = userAccessor.GetUserId();
        var userPortfolioIds = await context.Portfolios
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var query = context.PortfolioReconciliationResults
            .Where(x => userPortfolioIds.Contains(x.PortfolioId) && x.AlertRequired);

        if (request.PortfolioId.HasValue)
        {
            query = query.Where(x => x.PortfolioId == request.PortfolioId.Value);
        }

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

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToLowerInvariant();
            query = query.Where(x => x.Severity == severity);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            query = query.Where(x => x.Source == source);
        }

        var result = await query
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
            .ToPagedResultAsync(request.Paging, MaxPageSize, cancellationToken);

        return Result<PagedResult<PortfolioReconciliationResultDto>>.Success(result);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static bool TryValidateSeverity(string? severity, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(severity))
        {
            return true;
        }

        var normalized = severity.Trim();
        var supported = new[] { "info", "warning", "critical" };
        if (supported.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        error = ReconciliationApiErrors.InvalidSeverity(severity);
        return false;
    }
}
