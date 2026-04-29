using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlerts;

public sealed record GetPortfolioReconciliationAlertsQuery(
    Guid? PortfolioId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Severity = null,
    string? Source = null,
    PageQuery? Paging = null) : IRequest<Result<PagedResult<PortfolioReconciliationResultDto>>>;
