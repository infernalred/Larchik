using Larchik.Application.Contracts;
using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Reconciliation;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationHistory;

public sealed class GetPortfolioReconciliationHistoryQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfolioReconciliationHistoryQuery, Result<PagedResult<PortfolioReconciliationResultDto>>>
{
    private const int MaxPageSize = 200;

    public async Task<Result<PagedResult<PortfolioReconciliationResultDto>>> Handle(
        GetPortfolioReconciliationHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateSorting(request.SortBy, request.SortDirection, out var sortError))
        {
            return Result<PagedResult<PortfolioReconciliationResultDto>>.Failure(sortError!);
        }

        var userId = userAccessor.GetUserId();
        var userPortfolioIds = await context.Portfolios
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var query = context.PortfolioReconciliationResults
            .Where(x => userPortfolioIds.Contains(x.PortfolioId));

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

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToLowerInvariant();
            query = query.Where(x => x.Severity == severity);
        }

        if (request.AlertRequired.HasValue)
        {
            query = query.Where(x => x.AlertRequired == request.AlertRequired.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            query = query.Where(x => x.Source == source);
        }

        var sorted = ApplySorting(query, request.SortBy, request.SortDirection);
        var items = await sorted
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

        return Result<PagedResult<PortfolioReconciliationResultDto>>.Success(items);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static IQueryable<Larchik.Persistence.Entities.PortfolioReconciliationResult> ApplySorting(
        IQueryable<Larchik.Persistence.Entities.PortfolioReconciliationResult> query,
        string? sortBy,
        string? sortDirection)
    {
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? "statementDate" : sortBy.Trim();
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return (normalizedSortBy.ToLowerInvariant(), descending) switch
        {
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt),
            ("severity", true) => query.OrderByDescending(x => x.Severity).ThenByDescending(x => x.CreatedAt),
            ("severity", false) => query.OrderBy(x => x.Severity).ThenBy(x => x.CreatedAt),
            ("status", true) => query.OrderByDescending(x => x.Status).ThenByDescending(x => x.CreatedAt),
            ("status", false) => query.OrderBy(x => x.Status).ThenBy(x => x.CreatedAt),
            ("navdelta", true) => query.OrderByDescending(x => x.NavDelta).ThenByDescending(x => x.CreatedAt),
            ("navdelta", false) => query.OrderBy(x => x.NavDelta).ThenBy(x => x.CreatedAt),
            ("statementdate", false) => query.OrderBy(x => x.StatementDate).ThenBy(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.StatementDate).ThenByDescending(x => x.CreatedAt)
        };
    }

    private static bool TryValidateSorting(string? sortBy, string? sortDirection, out string? error)
    {
        error = null;

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var supportedSortBy = new[] { "statementDate", "createdAt", "severity", "status", "navDelta" };
            if (!supportedSortBy.Contains(sortBy.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                error = ReconciliationApiErrors.InvalidSortBy(sortBy);
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(sortDirection)
            && !string.Equals(sortDirection.Trim(), "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sortDirection.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            error = ReconciliationApiErrors.InvalidSortDirection(sortDirection);
            return false;
        }

        return true;
    }
}
