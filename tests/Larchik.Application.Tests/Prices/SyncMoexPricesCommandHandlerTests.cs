using System.Net;
using System.Text;
using Larchik.Application.Prices.SyncMoexPrices;
using Larchik.Application.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Larchik.Application.Tests.Prices;

public sealed class SyncMoexPricesCommandHandlerTests
{
    [Fact]
    public async Task Handle_Fails_WhenBoardsListIsEmpty()
    {
        await using var harness = new PriceSyncTestHarness();
        var handler = new SyncMoexPricesCommandHandler(
            harness.Context,
            new FakeHttpClientFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
            NullLogger<SyncMoexPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncMoexPricesCommand(new DateOnly(2026, 4, 20), Boards: []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MOEX boards list is empty", result.Error);
    }

    [Fact]
    public async Task Handle_InsertsPrice_ForEligibleMoexInstrument()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("SBER", currencyId: "RUB", priceSource: Larchik.Persistence.Entities.PriceSource.MOEX);
        await harness.Context.SaveChangesAsync();

        var factory = new FakeHttpClientFactory((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/history/engines/stock/markets/shares/boards/TQBR/securities.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "history": {
                        "columns": ["SECID","TRADEDATE","LEGALCLOSEPRICE","MARKETPRICE2","CLOSE","WAPRICE","LCLOSEPRICE","LAST","CURRENCYID","FACEVALUE","FACEUNIT","ACCINT"],
                        "data": [["SBER","2026-04-20",null,null,100.5,null,null,null,"RUB",null,null,null]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            if (url.Contains("/securities/SBER.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "boards": {
                        "columns": ["BOARDID","IS_TRADED"],
                        "data": [["TQBR",1]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var handler = new SyncMoexPricesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncMoexPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncMoexPricesCommand(new DateOnly(2026, 4, 20), Boards: ["TQBR"], BaseUrl: "https://moex.test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var price = await harness.Context.Prices.SingleAsync();
        Assert.Equal(instrumentId, price.InstrumentId);
        Assert.Equal(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), price.Date);
        Assert.Equal(100.5m, price.Value);
        Assert.Equal("RUB", price.CurrencyId);
        Assert.Equal("RUB", price.SourceCurrencyId);
        Assert.Equal("MOEX", price.Provider);

        var instrument = await harness.Context.Instruments.SingleAsync();
        Assert.True(instrument.IsTrading);
    }

    [Fact]
    public async Task Handle_UpdatesExistingPrice_ForSameInstrumentDateAndProvider()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("SBER", currencyId: "RUB", priceSource: Larchik.Persistence.Entities.PriceSource.MOEX);
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddPrice(instrumentId, date, 99m, "RUB", "MOEX", "RUB");
        await harness.Context.SaveChangesAsync();

        var factory = new FakeHttpClientFactory((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/history/engines/stock/markets/shares/boards/TQBR/securities.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "history": {
                        "columns": ["SECID","TRADEDATE","LEGALCLOSEPRICE","MARKETPRICE2","CLOSE","WAPRICE","LCLOSEPRICE","LAST","CURRENCYID","FACEVALUE","FACEUNIT","ACCINT"],
                        "data": [["SBER","2026-04-20",null,null,110.25,null,null,null,"RUB",null,null,null]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            if (url.Contains("/securities/SBER.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "boards": {
                        "columns": ["BOARDID","IS_TRADED"],
                        "data": [["TQBR",1]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var handler = new SyncMoexPricesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncMoexPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncMoexPricesCommand(new DateOnly(2026, 4, 20), Boards: ["TQBR"], BaseUrl: "https://moex.test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var prices = await harness.Context.Prices.ToListAsync();
        Assert.Single(prices);
        Assert.Equal(110.25m, prices[0].Value);
    }

    [Fact]
    public async Task Handle_MatchesPriceByInstrumentAlias()
    {
        await using var harness = new PriceSyncTestHarness();
        var instrumentId = harness.AddInstrument("SBER", currencyId: "RUB", priceSource: Larchik.Persistence.Entities.PriceSource.MOEX);
        harness.AddInstrumentAlias(instrumentId, "SBERP");
        await harness.Context.SaveChangesAsync();

        var factory = new FakeHttpClientFactory((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/history/engines/stock/markets/shares/boards/TQBR/securities.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "history": {
                        "columns": ["SECID","TRADEDATE","LEGALCLOSEPRICE","MARKETPRICE2","CLOSE","WAPRICE","LCLOSEPRICE","LAST","CURRENCYID","FACEVALUE","FACEUNIT","ACCINT"],
                        "data": [["SBERP","2026-04-20",null,null,101.25,null,null,null,"RUB",null,null,null]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            if (url.Contains("/securities/SBER.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "boards": {
                        "columns": ["BOARDID","IS_TRADED"],
                        "data": [["TQBR",1]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var handler = new SyncMoexPricesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncMoexPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncMoexPricesCommand(new DateOnly(2026, 4, 20), Boards: ["TQBR"], BaseUrl: "https://moex.test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var price = await harness.Context.Prices.SingleAsync();
        Assert.Equal(instrumentId, price.InstrumentId);
        Assert.Equal(101.25m, price.Value);
    }

    [Fact]
    public async Task Handle_FailsForBond_WhenRequiredFxRateIsMissing()
    {
        await using var harness = new PriceSyncTestHarness();
        harness.AddInstrument(
            "RU000A",
            currencyId: "RUB",
            type: Larchik.Persistence.Entities.InstrumentType.Bond,
            priceSource: Larchik.Persistence.Entities.PriceSource.MOEX);
        await harness.Context.SaveChangesAsync();

        var factory = new FakeHttpClientFactory((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/history/engines/stock/markets/shares/boards/TQCB/securities.json", StringComparison.Ordinal) ||
                url.Contains("/history/engines/stock/markets/bonds/boards/TQCB/securities.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "history": {
                        "columns": ["SECID","TRADEDATE","LEGALCLOSEPRICE","MARKETPRICE2","CLOSE","WAPRICE","LCLOSEPRICE","LAST","CURRENCYID","FACEVALUE","FACEUNIT","ACCINT"],
                        "data": [["RU000A","2026-04-20",50,null,null,null,null,null,"USD",1000,"USD",5]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            if (url.Contains("/securities/RU000A.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "boards": {
                        "columns": ["BOARDID","IS_TRADED"],
                        "data": [["TQCB",1]]
                      }
                    }
                    """, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var handler = new SyncMoexPricesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncMoexPricesCommandHandler>.Instance);

        var result = await handler.Handle(
            new SyncMoexPricesCommand(new DateOnly(2026, 4, 20), Boards: ["TQCB"], BaseUrl: "https://moex.test"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("FX rate is missing for MOEX bond price normalization", result.Error);
        Assert.Empty(await harness.Context.Prices.ToListAsync());
    }
}
