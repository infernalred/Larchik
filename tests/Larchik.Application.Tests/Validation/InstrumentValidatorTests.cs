using Larchik.Application.Models;
using Larchik.Application.Stocks.CreateStock;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Validation;

public class InstrumentValidatorTests
{
    private readonly InstrumentValidator validator = new();

    [Fact]
    public void Validate_RequiresIsin_ForEquityBondAndEtf()
    {
        foreach (var type in new[] { InstrumentType.Equity, InstrumentType.Bond, InstrumentType.Etf })
        {
            var result = validator.Validate(CreateModel(type, null));
            Assert.Contains(result.Errors, x => x.PropertyName == nameof(InstrumentModel.Isin));
        }
    }

    [Fact]
    public void Validate_AllowsMissingIsin_ForCurrency()
    {
        var result = validator.Validate(CreateModel(InstrumentType.Currency, null));

        Assert.DoesNotContain(result.Errors, x => x.PropertyName == nameof(InstrumentModel.Isin));
    }

    [Fact]
    public void Validate_RejectsTbankSource_ForRussianMarketInstrument()
    {
        var result = validator.Validate(CreateModel(
            InstrumentType.Equity,
            "RU0009029540",
            figi: "BBG004730N88",
            country: "ru",
            isTrading: true,
            priceSource: PriceSource.TBANK));

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(InstrumentModel.PriceSource));
    }

    private static InstrumentModel CreateModel(
        InstrumentType type,
        string? isin,
        string? figi = null,
        string? country = null,
        bool isTrading = false,
        PriceSource? priceSource = null) => new(
        Name: "Test instrument",
        Ticker: "TEST",
        Isin: isin,
        Figi: figi,
        Type: type,
        CurrencyId: "USD",
        CategoryId: 1,
        Exchange: null,
        Country: country,
        IsTrading: isTrading,
        PriceSource: priceSource);
}
