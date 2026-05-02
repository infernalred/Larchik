using Larchik.Application.Portfolios;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class PortfolioSummaryOptimizationRegressionTests
{
    [Fact]
    public async Task PortfolioSummary_CacheInvalidates_WhenOperationUpdatedAtChanges()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "RUB");
        var tradeDate = PortfolioAnalyticsTestHarness.SeedTimestamp;
        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", tradeDate, price: 1000m);
        await harness.SaveChangesAsync();

        var first = await harness.GetPortfolioSummaryAsync(portfolioId);
        Assert.Equal(1000m, first.NetInflowBase);

        await harness.Context.Operations
            .Where(x => x.PortfolioId == portfolioId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Price, 2000m)
                .SetProperty(x => x.UpdatedAt, tradeDate.AddHours(3)));

        var second = await harness.GetPortfolioSummaryAsync(portfolioId);
        Assert.Equal(2000m, second.NetInflowBase);
    }

    [Fact]
    public async Task PortfolioSummary_CacheInvalidates_WhenPositionPriceDataChanges()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "RUB");
        var tradeDate = PortfolioAnalyticsTestHarness.SeedTimestamp;
        var instrumentId = harness.AddInstrument("POS", "RUB");
        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", tradeDate, price: 10_000m);
        harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            "RUB",
            tradeDate,
            instrumentId,
            quantity: 10m,
            price: 100m);
        harness.AddPrice(instrumentId, "RUB", tradeDate, 100m);
        await harness.SaveChangesAsync();

        var first = await harness.GetPortfolioSummaryAsync(portfolioId);
        var firstMkt = first.Positions.Single(x => x.InstrumentId == instrumentId).MarketValueBase;

        await harness.Context.Prices
            .Where(p => p.InstrumentId == instrumentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Value, 200m)
                .SetProperty(p => p.UpdatedAt, tradeDate.AddHours(7)));

        var second = await harness.GetPortfolioSummaryAsync(portfolioId);
        var secondMkt = second.Positions.Single(x => x.InstrumentId == instrumentId).MarketValueBase;

        Assert.Equal(1000m, firstMkt);
        Assert.Equal(2000m, secondMkt);
    }

    [Fact]
    public async Task PortfolioSummary_CacheInvalidates_WhenFxRateDataChanges()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "RUB");
        var tradeDate = PortfolioAnalyticsTestHarness.SeedTimestamp;
        harness.AddOperation(portfolioId, OperationType.Deposit, "USD", tradeDate, price: 100m);
        harness.AddFxRate("USD", "RUB", tradeDate, 90m);
        await harness.SaveChangesAsync();

        var first = await harness.GetPortfolioSummaryAsync(portfolioId);
        var firstCashBase = first.CashBase;

        await harness.Context.FxRates
            .Where(x => x.BaseCurrencyId == "USD" && x.QuoteCurrencyId == "RUB")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Rate, 100m)
                .SetProperty(x => x.UpdatedAt, tradeDate.AddHours(5)));

        var second = await harness.GetPortfolioSummaryAsync(portfolioId);

        Assert.NotEqual(firstCashBase, second.CashBase);
    }

    /// <summary>
    /// Settlement currency (operation) differs from instrument quote currency; cache key must include FX for USD→RUB when reporting is RUB.
    /// </summary>
    [Fact]
    public async Task PortfolioSummary_CacheInvalidates_WhenQuoteCurrencyFxChanges_AndOperationSettledInOtherCurrency()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Cross", "RUB");
        var tradeDate = PortfolioAnalyticsTestHarness.SeedTimestamp;
        var instrumentId = harness.AddInstrument("USDEQ", "USD");
        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", tradeDate, price: 10_000m);
        harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            "RUB",
            tradeDate,
            instrumentId,
            quantity: 1m,
            price: 80m);
        harness.AddPrice(instrumentId, "USD", tradeDate, 100m);
        harness.AddFxRate("USD", "RUB", tradeDate, 90m);
        await harness.SaveChangesAsync();

        var first = await harness.GetPortfolioSummaryAsync(portfolioId);
        var firstMkt = first.Positions.Single(x => x.InstrumentId == instrumentId).MarketValueBase;

        await harness.Context.FxRates
            .Where(x => x.BaseCurrencyId == "USD" && x.QuoteCurrencyId == "RUB")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Rate, 100m)
                .SetProperty(x => x.UpdatedAt, tradeDate.AddHours(5)));

        var second = await harness.GetPortfolioSummaryAsync(portfolioId);
        var secondMkt = second.Positions.Single(x => x.InstrumentId == instrumentId).MarketValueBase;

        Assert.Equal(9000m, firstMkt);
        Assert.Equal(10_000m, secondMkt);
    }

    [Fact]
    public async Task PortfolioSnapshotSummaryBuilder_ReturnsNull_WhenLatestSnapshotDayNotAsOfDay()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "RUB");
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        harness.Context.PortfolioSnapshots.Add(new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            Date = yesterday,
            NavBase = 1m,
            PnlDayBase = 0,
            PnlMonthBase = 0,
            PnlYearBase = 0,
            CashBase = 1m
        });
        await harness.Context.SaveChangesAsync();

        var portfolio = await harness.Context.Portfolios.Include(x => x.Broker)
            .FirstAsync(x => x.Id == portfolioId);
        var data = new HistoricalDataLookup([], []);
        var result = await PortfolioSnapshotSummaryBuilder.TryBuildAsync(
            harness.Context,
            portfolio,
            [],
            new Dictionary<Guid, Instrument>(),
            data,
            "adjustingAvg",
            "RUB",
            DateTime.UtcNow,
            includeAnnualizedReturn: false,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadLatestPricesPerInstrument_LoadsOnlyRowsOnMaxDatePerInstrument()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var instrumentId = harness.AddInstrument("TST", "RUB");
        var baseDay = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 25; i++)
        {
            harness.AddPrice(instrumentId, "RUB", baseDay.AddDays(i), 10m + i, provider: "MOEX");
        }

        harness.AddPrice(instrumentId, "RUB", baseDay.AddDays(24), 99m, provider: "TBANK");
        await harness.SaveChangesAsync();

        var asOf = baseDay.AddDays(30);
        var prices = await PortfolioAnalyticsQueryHelper.LoadLatestPricesPerInstrumentAsync(
            harness.Context,
            [instrumentId],
            asOf,
            CancellationToken.None);

        Assert.Equal(2, prices.Count);
        Assert.All(prices, p => Assert.Equal(baseDay.AddDays(24).Date, p.Date.Date));
        Assert.Contains(prices, p => p.Provider == "MOEX");
        Assert.Contains(prices, p => p.Provider == "TBANK");
    }

    [Fact]
    public async Task PortfolioSnapshotSummary_MultiCurrencyCash_MatchesLiveCalculator()
    {
        await using var harness = new PortfolioAnalyticsTestHarness();
        var portfolioId = harness.AddPortfolio("Multi", "RUB");
        var day = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", day, price: 1000m);
        harness.AddOperation(portfolioId, OperationType.Deposit, "USD", day, price: 500m);
        harness.AddFxRate("USD", "RUB", day, 90m);
        harness.Context.PortfolioSnapshots.Add(new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            Date = day,
            NavBase = 0,
            PnlDayBase = 0,
            PnlMonthBase = 0,
            PnlYearBase = 0,
            CashBase = 0
        });
        await harness.SaveChangesAsync();

        var portfolio = await harness.Context.Portfolios
            .Include(x => x.Broker)
            .FirstAsync(x => x.Id == portfolioId);
        var asOf = day.AddHours(18);
        var operations = await harness.Context.Operations
            .Where(x => x.PortfolioId == portfolioId && x.TradeDate <= asOf)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();

        var analytics = await PortfolioAnalyticsQueryHelper.LoadAsync(
            harness.Context,
            operations,
            "RUB",
            asOf,
            additionalCurrencies: null,
            CancellationToken.None,
            useNarrowPriceHistory: true);

        var fromSnapshot = await PortfolioSnapshotSummaryBuilder.TryBuildAsync(
            harness.Context,
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            "adjustingAvg",
            "RUB",
            asOf,
            includeAnnualizedReturn: false,
            CancellationToken.None);

        Assert.NotNull(fromSnapshot);

        var live = new PortfolioAnalyticsCalculator().CalculateSummary(
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            "adjustingAvg",
            "RUB",
            asOf,
            includeAnnualizedReturn: false);

        var snapCash = fromSnapshot!.Cash.OrderBy(x => x.CurrencyId, StringComparer.Ordinal).ToList();
        var liveCash = live.Cash.OrderBy(x => x.CurrencyId, StringComparer.Ordinal).ToList();
        Assert.Equal(2, snapCash.Count);
        Assert.Equal(liveCash.Count, snapCash.Count);
        Assert.Equal(liveCash.Select(x => x.CurrencyId), snapCash.Select(x => x.CurrencyId));
        Assert.Equal(liveCash.Select(x => x.Amount), snapCash.Select(x => x.Amount));
        Assert.Equal(liveCash.Select(x => x.AmountInBase), snapCash.Select(x => x.AmountInBase));
    }
}
