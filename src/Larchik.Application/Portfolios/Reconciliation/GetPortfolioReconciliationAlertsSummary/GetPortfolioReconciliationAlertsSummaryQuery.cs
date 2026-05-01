namespace Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlertsSummary;

public sealed record GetPortfolioReconciliationAlertsSummaryQuery(
    DateTime? From = null,
    DateTime? To = null,
    string? Source = null);
