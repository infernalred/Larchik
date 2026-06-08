using Larchik.Application.Portfolios.GetPortfoliosSummary;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class GetPortfoliosSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_AggregatesMultiplePortfolioSummaries()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolio1Id = harness.AddPortfolio("Primary", "RUB");
        var portfolio2Id = harness.AddPortfolio("Secondary", "RUB");
        var instrumentId = harness.AddInstrument("LKOH", "RUB");
        var now = DateTime.UtcNow;

        harness.AddOperation(portfolio1Id, OperationType.Deposit, "RUB", now.AddDays(-10), price: 1000m);
        harness.AddOperation(portfolio1Id, OperationType.Buy, "RUB", now.AddDays(-9), instrumentId, quantity: 10m, price: 50m);
        harness.AddOperation(portfolio2Id, OperationType.Deposit, "RUB", now.AddDays(-8), price: 500m);
        harness.AddPrice(instrumentId, "RUB", now.AddDays(-1), 60m);
        await harness.SaveChangesAsync();

        var aggregate = await harness.GetPortfoliosSummaryAsync();
        var summary1 = await harness.GetPortfolioSummaryAsync(portfolio1Id);
        var summary2 = await harness.GetPortfolioSummaryAsync(portfolio2Id);

        Assert.Equal(2, aggregate.PortfolioCount);
        Assert.Equal("RUB", aggregate.ReportingCurrencyId);
        Assert.Equal("adjustingAvg", aggregate.ValuationMethod);
        Assert.Equal(summary1.NetInflowBase + summary2.NetInflowBase, aggregate.NetInflowBase);
        Assert.Equal(summary1.GrossDepositsBase + summary2.GrossDepositsBase, aggregate.GrossDepositsBase);
        Assert.Equal(summary1.GrossWithdrawalsBase + summary2.GrossWithdrawalsBase, aggregate.GrossWithdrawalsBase);
        Assert.Equal(summary1.CashBase + summary2.CashBase, aggregate.CashBase);
        Assert.Equal(summary1.PositionsValueBase + summary2.PositionsValueBase, aggregate.PositionsValueBase);
        Assert.Equal(summary1.RealizedBase + summary2.RealizedBase, aggregate.RealizedBase);
        Assert.Equal(summary1.UnrealizedBase + summary2.UnrealizedBase, aggregate.UnrealizedBase);
        Assert.Equal(summary1.NavBase + summary2.NavBase, aggregate.NavBase);
        Assert.Equal(summary1.PnlBase + summary2.PnlBase, aggregate.PnlBase);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenReportingCurrenciesDifferAndCurrencyIsNotSpecified()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        harness.AddPortfolio("Ruble", "RUB");
        harness.AddPortfolio("Dollar", "USD");
        await harness.SaveChangesAsync();

        var result = await harness.HandleAsync(new GetPortfoliosSummaryQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Portfolios use different reporting currencies. Specify the 'currency' query parameter.",
            result.Error);
    }

    [Fact]
    public async Task Handle_IgnoresPricesAfterCurrentAsOfDate()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Primary", "RUB");
        var instrumentId = harness.AddInstrument("SBER", "RUB");
        var now = DateTime.UtcNow;

        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", now.AddDays(-5), price: 1000m);
        harness.AddOperation(portfolioId, OperationType.Buy, "RUB", now.AddDays(-4), instrumentId, quantity: 10m, price: 50m);
        harness.AddPrice(instrumentId, "RUB", now.AddMinutes(-1), 60m);
        harness.AddPrice(instrumentId, "RUB", now.AddDays(1), 999m);
        await harness.SaveChangesAsync();

        var result = await harness.GetPortfoliosSummaryAsync();

        Assert.Equal(500m, result.CashBase);
        Assert.Equal(600m, result.PositionsValueBase);
        Assert.Equal(1100m, result.NavBase);
        Assert.Equal(100m, result.UnrealizedBase);
        Assert.Equal(100m, result.PnlBase);
    }

    [Fact]
    public async Task Handle_ConvertsCrossCurrencyOperationsUsingHistoricalFxRate()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Multi FX", "RUB");
        var instrumentId = harness.AddInstrument("AAPL", "USD");
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var valuationDate = tradeDate.AddDays(1);

        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", tradeDate, price: 10000m);
        harness.AddOperation(portfolioId, OperationType.Buy, "RUB", tradeDate, instrumentId, quantity: 1m, price: 80m);
        harness.AddPrice(instrumentId, "USD", valuationDate, 100m);
        harness.AddFxRate("USD", "RUB", tradeDate, 80m);
        harness.AddFxRate("USD", "RUB", valuationDate, 90m);
        await harness.SaveChangesAsync();

        var result = await harness.GetPortfoliosSummaryAsync();

        Assert.Equal(8910m, result.UnrealizedBase);
        Assert.Equal(18920m, result.NavBase);
        Assert.Equal(8910m, result.PnlBase);
    }

    [Fact]
    public async Task Handle_AccountsForSellFeeInRealizedAndCashBalances()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Fees", "RUB");
        var instrumentId = harness.AddInstrument("GAZP", "RUB");
        var now = DateTime.UtcNow;

        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", now.AddDays(-5), price: 2000m);
        harness.AddOperation(portfolioId, OperationType.Buy, "RUB", now.AddDays(-4), instrumentId, quantity: 10m, price: 100m);
        harness.AddOperation(portfolioId, OperationType.Sell, "RUB", now.AddDays(-3), instrumentId, quantity: 4m, price: 120m, fee: 10m);
        harness.AddPrice(instrumentId, "RUB", now.AddDays(-1), 110m);
        await harness.SaveChangesAsync();

        var result = await harness.GetPortfoliosSummaryAsync();

        Assert.Equal(1470m, result.CashBase);
        Assert.Equal(660m, result.PositionsValueBase);
        Assert.Equal(70m, result.RealizedBase);
        Assert.Equal(60m, result.UnrealizedBase);
        Assert.Equal(130m, result.PnlBase);
    }

    [Fact]
    public async Task Handle_UsesCostBasisForPositionValue_WhenAsOfPriceIsMissing()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("No Price", "RUB");
        var instrumentId = harness.AddInstrument("NVTK", "RUB");
        var now = DateTime.UtcNow;

        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", now.AddDays(-5), price: 1000m);
        harness.AddOperation(portfolioId, OperationType.Buy, "RUB", now.AddDays(-4), instrumentId, quantity: 10m, price: 50m);
        await harness.SaveChangesAsync();

        var result = await harness.GetPortfoliosSummaryAsync();

        Assert.Equal(500m, result.CashBase);
        Assert.Equal(500m, result.PositionsValueBase);
        Assert.Equal(0m, result.UnrealizedBase);
        Assert.Equal(0m, result.PnlBase);
    }
}
