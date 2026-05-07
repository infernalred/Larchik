using Larchik.Application.Portfolios;
using Larchik.Application.Portfolios.GetAggregatePortfolioPerformance;
using Larchik.Application.Portfolios.GetAggregatePortfolioSummary;
using Larchik.Application.Portfolios.GetPortfolioPerformance;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class PortfolioAnalyticsQueryHandlersTests
{
    [Fact]
    public void NormalizeMaxPriceDateUtc_ReturnsUtcEndOfDay_ForUnspecifiedDate()
    {
        var rawDate = new DateTime(2026, 4, 20, 13, 30, 0, DateTimeKind.Unspecified);

        var normalized = PortfolioAnalyticsQueryHelper.NormalizeMaxPriceDateUtc(rawDate);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(new DateTime(2026, 4, 20, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9999), normalized);
    }

    [Fact]
    public async Task AggregateSummary_AggregatesSinglePortfolioSummaries()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolio1Id = harness.AddPortfolio("Primary", "RUB");
        var portfolio2Id = harness.AddPortfolio("Secondary", "RUB");
        var instrumentId = harness.AddInstrument("LKOH", "RUB");
        var now = DateTime.UtcNow;

        harness.AddOperation(portfolio1Id, Larchik.Persistence.Entities.OperationType.Deposit, "RUB", now.AddDays(-10), price: 1000m);
        harness.AddOperation(portfolio1Id, Larchik.Persistence.Entities.OperationType.Buy, "RUB", now.AddDays(-9), instrumentId, quantity: 10m, price: 50m);
        harness.AddOperation(portfolio2Id, Larchik.Persistence.Entities.OperationType.Deposit, "RUB", now.AddDays(-8), price: 500m);
        harness.AddPrice(instrumentId, "RUB", now.AddDays(-1), 60m);
        await harness.SaveChangesAsync();

        var aggregate = await harness.GetAggregatePortfolioSummaryAsync();
        var summary1 = await harness.GetPortfolioSummaryAsync(portfolio1Id);
        var summary2 = await harness.GetPortfolioSummaryAsync(portfolio2Id);

        Assert.Equal("Все счета", aggregate.Name);
        Assert.Equal(summary1.NetInflowBase + summary2.NetInflowBase, aggregate.NetInflowBase);
        Assert.Equal(summary1.CashBase + summary2.CashBase, aggregate.CashBase);
        Assert.Equal(summary1.PositionsValueBase + summary2.PositionsValueBase, aggregate.PositionsValueBase);
        Assert.Equal(summary1.NavBase + summary2.NavBase, aggregate.NavBase);
        Assert.Equal(summary1.PnlBase + summary2.PnlBase, aggregate.PnlBase);
    }

    [Fact]
    public async Task AggregateSummary_Fails_WhenReportingCurrenciesDifferAndCurrencyIsNotSpecified()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        harness.AddPortfolio("Ruble", "RUB");
        harness.AddPortfolio("Dollar", "USD");
        await harness.SaveChangesAsync();

        var result = await harness.HandleAggregateSummaryAsync(new GetAggregatePortfolioSummaryQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Portfolios use different reporting currencies. Specify the 'currency' query parameter.",
            result.Error);
    }

    [Fact]
    public async Task PortfolioPerformance_ReturnsEmpty_WhenPortfolioHasNoOperations()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "RUB");
        await harness.SaveChangesAsync();

        var series = await harness.GetPortfolioPerformanceAsync(portfolioId);

        Assert.Empty(series);
    }

    [Fact]
    public async Task PortfolioPerformance_ReturnsNull_ForForeignPortfolio()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Foreign", "RUB", userId: PortfolioAnalyticsTestHarness.OtherUserId);
        await harness.SaveChangesAsync();

        var result = await harness.HandlePortfolioPerformanceAsync(new GetPortfolioPerformanceQuery(portfolioId));

        Assert.Null(result);
    }

    [Fact]
    public async Task PortfolioPerformance_UsesWeightedFlows_ForMonthlyReturn()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "RUB");
        var instrumentId = harness.AddInstrument("SBER", "RUB");
        var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        harness.AddOperation(portfolioId, Larchik.Persistence.Entities.OperationType.Deposit, "RUB", start.AddMonths(-1), price: 1m);
        harness.AddOperation(portfolioId, Larchik.Persistence.Entities.OperationType.Deposit, "RUB", start.AddDays(14), price: 1000m);
        harness.AddOperation(
            portfolioId,
            Larchik.Persistence.Entities.OperationType.Buy,
            "RUB",
            start.AddDays(14),
            instrumentId,
            quantity: 10m,
            price: 100m);
        harness.AddPrice(instrumentId, "RUB", new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), 110m);
        await harness.SaveChangesAsync();

        var series = await harness.GetPortfolioPerformanceAsync(
            portfolioId,
            from: start,
            to: new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));
        var february = Assert.Single(series);

        Assert.Equal(100m, february.PnlBase);
        Assert.Equal(1000m, february.NetInflowBase);
        Assert.Equal(100m / 501m, february.ReturnPct);
    }

    [Fact]
    public async Task PortfolioPerformance_UsesCostFallback_WhenPriceIsMissing()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "RUB");
        var instrumentId = harness.AddInstrument("MSFT", "RUB");
        var january = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        harness.AddOperation(portfolioId, Larchik.Persistence.Entities.OperationType.Deposit, "RUB", january, price: 1000m);
        harness.AddOperation(
            portfolioId,
            Larchik.Persistence.Entities.OperationType.Buy,
            "RUB",
            january,
            instrumentId,
            quantity: 10m,
            price: 100m);
        harness.AddPrice(instrumentId, "RUB", new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), 110m);
        await harness.SaveChangesAsync();

        var series = await harness.GetPortfolioPerformanceAsync(
            portfolioId,
            from: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc));
        var januaryPoint = series.Single(x => x.Period == "2026-01");
        var februaryPoint = series.Single(x => x.Period == "2026-02");

        Assert.Equal(1000m, januaryPoint.EndNavBase);
        Assert.Equal(0m, januaryPoint.PnlBase);
        Assert.Equal(0m, januaryPoint.ReturnPct);
        Assert.Equal(1000m, februaryPoint.StartNavBase);
        Assert.Equal(1100m, februaryPoint.EndNavBase);
        Assert.Equal(100m, februaryPoint.PnlBase);
    }

    [Fact]
    public async Task AggregatePerformance_AggregatesPerPortfolioMonthlySeries()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolio1Id = harness.AddPortfolio("Primary", "RUB");
        var portfolio2Id = harness.AddPortfolio("Secondary", "RUB");
        var instrumentId = harness.AddInstrument("SBER", "RUB");
        var start = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        harness.AddOperation(portfolio1Id, Larchik.Persistence.Entities.OperationType.Deposit, "RUB", start, price: 1000m);
        harness.AddOperation(portfolio1Id, Larchik.Persistence.Entities.OperationType.Buy, "RUB", start.AddDays(1), instrumentId, quantity: 10m, price: 50m);
        harness.AddOperation(portfolio2Id, Larchik.Persistence.Entities.OperationType.Deposit, "RUB", start, price: 500m);
        harness.AddPrice(instrumentId, "RUB", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), 60m);
        harness.AddPrice(instrumentId, "RUB", new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), 70m);
        await harness.SaveChangesAsync();

        var aggregate = await harness.GetAggregatePerformanceAsync(from: new DateTime(2026, 1, 1), to: new DateTime(2026, 2, 28));
        var performance1 = await harness.GetPortfolioPerformanceAsync(portfolio1Id, from: new DateTime(2026, 1, 1), to: new DateTime(2026, 2, 28));
        var performance2 = await harness.GetPortfolioPerformanceAsync(portfolio2Id, from: new DateTime(2026, 1, 1), to: new DateTime(2026, 2, 28));

        Assert.Equal(2, aggregate.Count);

        foreach (var point in aggregate)
        {
            var p1 = performance1.Single(x => x.Period == point.Period);
            var p2 = performance2.Single(x => x.Period == point.Period);

            Assert.Equal(p1.StartNavBase + p2.StartNavBase, point.StartNavBase);
            Assert.Equal(p1.EndNavBase + p2.EndNavBase, point.EndNavBase);
            Assert.Equal(p1.NetInflowBase + p2.NetInflowBase, point.NetInflowBase);
            Assert.Equal(p1.PnlBase + p2.PnlBase, point.PnlBase);
        }
    }

    [Fact]
    public async Task AggregatePerformance_Fails_WhenReportingCurrenciesDifferAndCurrencyIsNotSpecified()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        harness.AddPortfolio("Ruble", "RUB");
        harness.AddPortfolio("Dollar", "USD");
        await harness.SaveChangesAsync();

        var result = await harness.HandleAggregatePerformanceAsync(new GetAggregatePortfolioPerformanceQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Portfolios use different reporting currencies. Specify the 'currency' query parameter.",
            result.Error);
    }
}
