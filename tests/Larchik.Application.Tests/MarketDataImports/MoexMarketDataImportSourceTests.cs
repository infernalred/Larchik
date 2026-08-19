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

public sealed class MoexMarketDataImportSourceTests
{
    [Fact]
    public async Task ResolveAsync_UsesExactIsinAndPrimaryBoardMetadata()
    {
        var calls = new List<string>();
        var factory = new FakeHttpClientFactory((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            calls.Add(url);
            var json = url.Contains("/securities/YDEX.json", StringComparison.Ordinal)
                ? """
                  {"boards":{"columns":["secid","boardid","market","engine","is_traded","history_from","listed_from","is_primary","currencyid"],"data":[["YDEX","TQBR","shares","stock",1,"2024-07-08","2024-07-08",1,"SUR"]]}}
                  """
                : """
                  {"securities":{"columns":["secid","shortname","name","isin","is_traded","type","group","primary_boardid"],"data":[["OTHER","Other","Other","US0000000001",1,"common_share","stock_shares","TQBR"],["YDEX","Яндекс","МКПАО Яндекс","RU000A107T19",1,"common_share","stock_shares","TQBR"]]}}
                  """;
            return Task.FromResult(Json(json));
        });
        var source = CreateSource(factory);

        var result = await source.ResolveAsync("RU000A107T19", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal("YDEX", result.Value.Ticker);
        Assert.Equal(InstrumentType.Equity, result.Value.Type);
        Assert.Equal("RUB", result.Value.CurrencyId);
        Assert.Equal("MOEX", result.Value.ExchangeId);
        Assert.Equal("RU", result.Value.CountryId);
        Assert.Equal("TQBR", result.Value.Board);
        Assert.Equal("stock", result.Value.Engine);
        Assert.Equal("shares", result.Value.Market);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task LoadPricesAsync_ConvertsBondPercentAndAccruedInterestToDirtyPrice()
    {
        var factory = new FakeHttpClientFactory((_, _) => Task.FromResult(Json("""
            {"history":{"columns":["TRADEDATE","SECID","LEGALCLOSEPRICE","CURRENCYID","FACEVALUE","FACEUNIT","ACCINT"],"data":[["2026-08-14","RU000A10FTR1",101.25,"SUR",1000,"SUR",12.5]]}}
            """)));
        var source = CreateSource(factory);

        var result = await source.LoadPricesAsync(
            new MarketDataImportPriceLoadRequest(
                Guid.NewGuid(), "RU000A10FTR1", "RU000A10FTR1", null, InstrumentType.Bond, "RUB",
                "RU000A10FTR1", "TQCB", "stock", "bonds", new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var point = Assert.Single(result.Value!);
        Assert.Equal(1025m, point.Value);
        Assert.Equal("RUB", point.CurrencyId);
        Assert.Equal("RUB", point.SourceCurrencyId);
    }

    private static MoexMarketDataImportSource CreateSource(IHttpClientFactory factory) => new(
        factory,
        Options.Create(new MarketDataImportSourceOptions
        {
            Moex = new MoexMarketDataImportSourceOptions { BaseUrl = "https://moex.test/iss" }
        }),
        NullLogger<MoexMarketDataImportSource>.Instance);

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
