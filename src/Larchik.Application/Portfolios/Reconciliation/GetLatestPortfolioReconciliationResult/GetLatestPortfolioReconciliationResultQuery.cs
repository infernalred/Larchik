namespace Larchik.Application.Portfolios.Reconciliation.GetLatestPortfolioReconciliationResult;

public sealed record GetLatestPortfolioReconciliationResultQuery(
    Guid PortfolioId,
    string? Source = null);
