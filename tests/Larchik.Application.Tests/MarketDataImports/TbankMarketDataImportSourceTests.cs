using System.Net;
using System.Text;
using Larchik.Application.MarketDataImports.Processing;
using Larchik.Application.Tests.TestDoubles;
using Larchik.Infrastructure.MarketDataImports;
using Larchik.Persistence.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Larchik.Application.Tests.MarketDataImports;

public sealed class TbankMarketDataImportSourceTests
{
    [Fact]
    public async Task ResolveAsync_SelectsExactIsinAndKeepsFigiAsExistingInstrumentIdentifier()
    {
        var authorization = string.Empty;
        var factory = new FakeHttpClientFactory((request, _) =>
        {
            authorization = request.Headers.Authorization?.ToString() ?? string.Empty;
            return Task.FromResult(Json("""
                {"instruments":[{"figi":"BBG006L8G4H1","ticker":"YDEX","classCode":"TQBR","isin":"RU000A107T19","name":"МКПАО Яндекс","instrumentType":"share","currency":"rub","apiTradeAvailableFlag":true}]}
                """));
        });
        var source = CreateSource(factory);

        var result = await source.ResolveAsync("RU000A107T19", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal("BBG006L8G4H1", result.Value.Figi);
        Assert.Equal("BBG006L8G4H1", result.Value.SourceInstrumentCode);
        Assert.Equal(InstrumentType.Equity, result.Value.Type);
        Assert.Equal("MOEX", result.Value.ExchangeId);
        Assert.Equal("Bearer secret", authorization);
    }

    [Fact]
    public async Task LoadPricesAsync_LoadsEveryDailyCandleInRequestedRange()
    {
        var factory = new FakeHttpClientFactory((_, _) => Task.FromResult(Json("""
            {"candles":[
              {"time":"2026-08-13T00:00:00Z","close":{"units":"10","nano":500000000}},
              {"time":"2026-08-14T00:00:00Z","close":{"units":"11","nano":250000000}}
            ]}
            """)));
        var source = CreateSource(factory);

        var result = await source.LoadPricesAsync(
            new MarketDataImportPriceLoadRequest(
                Guid.NewGuid(), "RU000A107T19", "YDEX", "BBG006L8G4H1", InstrumentType.Equity, "RUB",
                "BBG006L8G4H1", "TQBR", null, null, new DateOnly(2026, 8, 13), new DateOnly(2026, 8, 14)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Collection(
            result.Value!.OrderBy(x => x.Date),
            x => Assert.Equal(10.5m, x.Value),
            x => Assert.Equal(11.25m, x.Value));
    }

    private static TbankMarketDataImportSource CreateSource(IHttpClientFactory factory) => new(
        factory,
        Options.Create(new MarketDataImportSourceOptions
        {
            Tbank = new TbankMarketDataImportSourceOptions
            {
                Token = "secret",
                FindInstrumentBaseUrl = "https://tbank.test/FindInstrument",
                CandlesBaseUrl = "https://tbank.test/GetCandles",
                AccruedInterestsBaseUrl = "https://tbank.test/GetAccruedInterests"
            }
        }),
        NullLogger<TbankMarketDataImportSource>.Instance);

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
