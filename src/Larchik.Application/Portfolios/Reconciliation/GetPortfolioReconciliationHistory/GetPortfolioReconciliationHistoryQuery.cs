using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationHistory;

public sealed record GetPortfolioReconciliationHistoryQuery(
    Guid? PortfolioId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Status = null,
    string? Severity = null,
    bool? AlertRequired = null,
    string? Source = null,
    string? SortBy = null,
    string? SortDirection = null,
    PageQuery? Paging = null) : IRequest<Result<PagedResult<PortfolioReconciliationResultDto>>>;
