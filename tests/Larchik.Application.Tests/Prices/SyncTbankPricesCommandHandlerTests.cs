using System.Net;
using System.Text;
using Larchik.Application.Prices.SyncTbankPrices;
using Larchik.Application.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Larchik.Application.Tests.Prices;

public sealed class SyncTbankPricesCommandHandlerTests
{
    [Fact]
    public async Task Handle_Fails_WhenTokenIsMissing()
    {
        await using var harness = new PriceSyncTestHarness();
        var handler = new SyncTbankPricesCommandHandler(
            harness.Context,
            new FakeHttpClientFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
            NullLogger<SyncTbankPricesCommandHandler>.Instance);

        var result = await handler.Handle(new SyncTbankPricesCommand(new DateOnly(2026, 4, 20), Token: null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TBANK token is not configured", result.Error);
    }

    [Fact]
    public async Task Handle_InsertsLatestAvailableCandle_ForEligibleInstrument()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("AAPL", currencyId: "USD", figi: "FIGI123", priceSource: Larchik.Persistence.Entities.PriceSource.TBANK);
        await harness.Context.SaveChangesAsync();

        var factory = new FakeHttpClientFactory(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("\"instrumentId\":\"FIGI123\"", body);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candles": [
                    { "time": "2026-04-19T20:00:00Z", "close": { "units": "98", "nano": "0" } },
                    { "time": "2026-04-20T20:00:00Z", "close": { "units": "101", "nano": "500000000" } }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            };
        });

        var handler = new SyncTbankPricesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncTbankPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncTbankPricesCommand(new DateOnly(2026, 4, 20), Token: "secret", BaseUrl: "https://tbank.test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var price = await harness.Context.Prices.AsNoTracking().SingleAsync();
        Assert.Equal(instrumentId, price.InstrumentId);
        Assert.Equal(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), price.Date);
        Assert.Equal(101.5m, price.Value);
        Assert.Equal("USD", price.CurrencyId);
        Assert.Equal("USD", price.SourceCurrencyId);
        Assert.Equal("TBANK", price.Provider);
    }

    [Fact]
    public async Task Handle_UpdatesExistingPrice_ForSameInstrumentDateAndProvider()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("AAPL", currencyId: "USD", figi: "FIGI123", priceSource: Larchik.Persistence.Entities.PriceSource.TBANK);
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddPrice(instrumentId, date, 99m, "USD", "TBANK", "USD");
        await harness.Context.SaveChangesAsync();

        var factory = new FakeHttpClientFactory((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candles": [
                    { "time": "2026-04-20T20:00:00Z", "close": { "units": "105", "nano": "0" } }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            }));

        var handler = new SyncTbankPricesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncTbankPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncTbankPricesCommand(new DateOnly(2026, 4, 20), Token: "secret", BaseUrl: "https://tbank.test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var prices = await harness.Context.Prices.AsNoTracking().ToListAsync();
        Assert.Single(prices);
        Assert.Equal(105m, prices[0].Value);
    }

    [Fact]
    public async Task Handle_UsesActiveListingFigi_ForRequest()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("AAPL", currencyId: "USD", figi: "CURRENT_FIGI", priceSource: Larchik.Persistence.Entities.PriceSource.TBANK);
        var date = new DateOnly(2026, 4, 20);
        var effectiveFrom = date.AddDays(-10).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        harness.AddListingHistory(instrumentId, "AAPL", "USD", effectiveFrom, figi: "HISTORICAL_FIGI");
        await harness.Context.SaveChangesAsync();

        var requestedBodies = new List<string>();
        var factory = new FakeHttpClientFactory(async (request, cancellationToken) =>
        {
            requestedBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candles": [
                    { "time": "2026-04-20T20:00:00Z", "close": { "units": "101", "nano": "0" } }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            };
        });

        var handler = new SyncTbankPricesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncTbankPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncTbankPricesCommand(date, Token: "secret", BaseUrl: "https://tbank.test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(requestedBodies);
        Assert.Contains("\"instrumentId\":\"HISTORICAL_FIGI\"", requestedBodies[0]);
    }
}
