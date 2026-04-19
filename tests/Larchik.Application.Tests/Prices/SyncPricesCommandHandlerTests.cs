using Larchik.Application.Models;
using Larchik.Application.Prices.SyncPrices;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Prices;

public sealed class SyncPricesCommandHandlerTests
{
    [Fact]
    public async Task Handle_InsertsAndNormalizesPrice_ToInstrumentCurrency()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("AAPL", currencyId: "RUB");
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddListingHistory(instrumentId, "AAPL", "USD", date.AddDays(-10));
        harness.AddFxRate("USD", "RUB", date, 80m);
        await harness.Context.SaveChangesAsync();

        var handler = new SyncPricesCommandHandler(harness.Context);
        var result = await handler.Handle(
            new SyncPricesCommand(
                [new PriceModel(instrumentId, date, 10m, " usd ", " test_provider ")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);

        var price = await harness.Context.Prices.AsNoTracking().SingleAsync();
        Assert.Equal(instrumentId, price.InstrumentId);
        Assert.Equal(date, price.Date);
        Assert.Equal(800m, price.Value);
        Assert.Equal("RUB", price.CurrencyId);
        Assert.Equal("USD", price.SourceCurrencyId);
        Assert.Equal("TEST_PROVIDER", price.Provider);
    }

    [Fact]
    public async Task Handle_UpdatesExistingPrice_ForSameInstrumentDateAndProvider()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("SBER", currencyId: "RUB");
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddPrice(instrumentId, date, 100m, "RUB", "MOEX", "RUB");
        await harness.Context.SaveChangesAsync();

        var handler = new SyncPricesCommandHandler(harness.Context);
        var result = await handler.Handle(
            new SyncPricesCommand(
                [new PriceModel(instrumentId, date, 120m, "RUB", "moex")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var prices = await harness.Context.Prices.AsNoTracking().ToListAsync();
        Assert.Single(prices);
        Assert.Equal(120m, prices[0].Value);
        Assert.Equal("MOEX", prices[0].Provider);
    }

    [Fact]
    public async Task Handle_NormalizesStoredDate_ToUtcDateBoundary()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("AAPL", currencyId: "USD");
        var inputDate = new DateTime(2026, 4, 20, 15, 30, 0, DateTimeKind.Utc);
        harness.AddListingHistory(instrumentId, "AAPL", "USD", inputDate.AddDays(-10));
        await harness.Context.SaveChangesAsync();

        var handler = new SyncPricesCommandHandler(harness.Context);
        var result = await handler.Handle(
            new SyncPricesCommand(
                [new PriceModel(instrumentId, inputDate, 10m, "USD", "TEST")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var price = await harness.Context.Prices.AsNoTracking().SingleAsync();
        Assert.Equal(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), price.Date);
    }

    [Fact]
    public async Task Handle_Fails_WhenPriceCurrencyMismatchesActiveListing()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("AAPL", currencyId: "USD");
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddListingHistory(instrumentId, "AAPL", "USD", date.AddDays(-10));
        await harness.Context.SaveChangesAsync();

        var handler = new SyncPricesCommandHandler(harness.Context);
        var result = await handler.Handle(
            new SyncPricesCommand(
                [new PriceModel(instrumentId, date, 10m, "RUB", "TEST")]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Price currency mismatch with active listing", result.Error);
    }

    [Fact]
    public async Task Handle_Fails_WhenFxRateForNormalizationIsMissing()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("AAPL", currencyId: "RUB");
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddListingHistory(instrumentId, "AAPL", "USD", date.AddDays(-10));
        await harness.Context.SaveChangesAsync();

        var handler = new SyncPricesCommandHandler(harness.Context);
        var result = await handler.Handle(
            new SyncPricesCommand(
                [new PriceModel(instrumentId, date, 10m, "USD", "TEST")]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("FX rate is missing for price normalization", result.Error);
    }
}
