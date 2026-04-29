using Larchik.Application.Models;
using Larchik.Application.Portfolios.Reconciliation;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class PortfolioReconciliationHelperTests
{
    [Fact]
    public void Compare_ReturnsWithinTolerance_WhenDeltasAreSmall()
    {
        var summary = new PortfolioSummaryDto
        {
            ReportingCurrencyId = "RUB",
            NavBase = 1000.005m,
            CashBase = 600.004m,
            PositionsValueBase = 400.001m
        };
        var statement = new BrokerageStatementSnapshot(
            new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            NavBase: 1000m,
            CashBase: 600m,
            PositionsValueBase: 400m);

        var result = PortfolioReconciliationHelper.Compare(summary, statement, tolerance: 0.01m);

        Assert.True(result.IsWithinTolerance);
    }

    [Fact]
    public void Compare_ReturnsMismatch_WhenAnyDeltaExceedsTolerance()
    {
        var summary = new PortfolioSummaryDto
        {
            ReportingCurrencyId = "RUB",
            NavBase = 1020m,
            CashBase = 590m,
            PositionsValueBase = 430m
        };
        var statement = new BrokerageStatementSnapshot(
            new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            NavBase: 1000m,
            CashBase: 600m,
            PositionsValueBase: 400m);

        var result = PortfolioReconciliationHelper.Compare(summary, statement, tolerance: 1m);

        Assert.False(result.IsWithinTolerance);
        Assert.Equal(20m, result.NavDelta);
        Assert.Equal(-10m, result.CashDelta);
        Assert.Equal(30m, result.PositionsDelta);
    }
}
