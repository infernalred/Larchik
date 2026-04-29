using Larchik.Application.Models;

namespace Larchik.Application.Portfolios.Reconciliation;

public static class PortfolioReconciliationHelper
{
    public static PortfolioReconciliationResult Compare(
        PortfolioSummaryDto internalSummary,
        BrokerageStatementSnapshot statement,
        decimal tolerance = 0.01m)
    {
        var normalizedTolerance = tolerance < 0 ? 0 : tolerance;
        var navDelta = internalSummary.NavBase - statement.NavBase;
        var cashDelta = internalSummary.CashBase - statement.CashBase;
        var positionsDelta = internalSummary.PositionsValueBase - statement.PositionsValueBase;

        return new PortfolioReconciliationResult(
            statement.AsOfDateUtc,
            internalSummary.ReportingCurrencyId,
            navDelta,
            cashDelta,
            positionsDelta,
            Math.Abs(navDelta) <= normalizedTolerance &&
            Math.Abs(cashDelta) <= normalizedTolerance &&
            Math.Abs(positionsDelta) <= normalizedTolerance);
    }
}

public sealed record BrokerageStatementSnapshot(
    DateTime AsOfDateUtc,
    decimal NavBase,
    decimal CashBase,
    decimal PositionsValueBase);

public sealed record PortfolioReconciliationResult(
    DateTime AsOfDateUtc,
    string CurrencyId,
    decimal NavDelta,
    decimal CashDelta,
    decimal PositionsDelta,
    bool IsWithinTolerance);
