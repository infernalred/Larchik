using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlertsSummary;

public sealed record GetPortfolioReconciliationAlertsSummaryQuery(
    DateTime? From = null,
    DateTime? To = null,
    string? Source = null) : IRequest<Result<PortfolioReconciliationAlertsSummaryDto>>;
