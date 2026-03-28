using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Valuation;

public class HistoricalDataLookupSmokeTests
{
    [Fact]
    public void GetPrice_PrefersMoexOverTbank_OnSameDate()
    {
        var instrumentId = Guid.NewGuid();
        var asOfDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var lookup = new HistoricalDataLookup(
        [
            new Price
            {
                InstrumentId = instrumentId,
                Date = asOfDate,
                Value = 101m,
                CurrencyId = "RUB",
                Provider = "TBANK",
                CreatedAt = asOfDate.AddHours(2)
            },
            new Price
            {
                InstrumentId = instrumentId,
                Date = asOfDate,
                Value = 100m,
                CurrencyId = "RUB",
                Provider = "MOEX",
                CreatedAt = asOfDate.AddHours(1)
            }
        ], []);

        var price = lookup.GetPrice(instrumentId, asOfDate);

        Assert.NotNull(price);
        Assert.Equal(100m, price!.Value);
        Assert.Equal("MOEX", price.Provider);
    }

    [Fact]
    public void GetPrice_FallsBackToTbank_WhenMoexPriceIsMissing()
    {
        var instrumentId = Guid.NewGuid();
        var asOfDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var lookup = new HistoricalDataLookup(
        [
            new Price
            {
                InstrumentId = instrumentId,
                Date = asOfDate,
                Value = 57.37m,
                CurrencyId = "USD",
                Provider = "TBANK",
                CreatedAt = asOfDate
            }
        ], []);

        var price = lookup.GetPrice(instrumentId, asOfDate);

        Assert.NotNull(price);
        Assert.Equal(57.37m, price!.Value);
        Assert.Equal("TBANK", price.Provider);
    }

    [Fact]
    public void Convert_PrefersMarketMoexRate_OverMarketTbankAndCbr()
    {
        var asOfDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var lookup = new HistoricalDataLookup(
            [],
            [
                new FxRate
                {
                    BaseCurrencyId = "USD",
                    QuoteCurrencyId = "RUB",
                    Date = asOfDate,
                    Rate = 95m,
                    Source = "MARKET_TBANK",
                    CreatedAt = asOfDate
                },
                new FxRate
                {
                    BaseCurrencyId = "USD",
                    QuoteCurrencyId = "RUB",
                    Date = asOfDate,
                    Rate = 90m,
                    Source = "CBR",
                    CreatedAt = asOfDate
                },
                new FxRate
                {
                    BaseCurrencyId = "USD",
                    QuoteCurrencyId = "RUB",
                    Date = asOfDate,
                    Rate = 80m,
                    Source = "MARKET_MOEX",
                    CreatedAt = asOfDate
                }
            ]);

        var converted = lookup.Convert(2m, "USD", "RUB", asOfDate);
        var inverse = lookup.Convert(160m, "RUB", "USD", asOfDate);

        Assert.Equal(160m, converted);
        Assert.Equal(2m, inverse);
    }
}
