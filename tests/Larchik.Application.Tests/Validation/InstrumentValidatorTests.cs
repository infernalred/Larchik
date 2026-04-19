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

    private static InstrumentModel CreateModel(InstrumentType type, string? isin) => new(
        Name: "Test instrument",
        Ticker: "TEST",
        Isin: isin,
        Figi: null,
        Type: type,
        CurrencyId: "USD",
        CategoryId: 1,
        Exchange: null,
        Country: null,
        IsTrading: false,
        PriceSource: null);
}
