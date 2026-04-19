using Larchik.Application.Models;
using Larchik.Application.Stocks;
using Xunit;

namespace Larchik.Application.Tests.Stocks;

public class InstrumentQueryHelperTests
{
    [Theory]
    [InlineData("GAZP-RM", "GAZP RM")]
    [InlineData("GAZP_RM", "GAZP RM")]
    [InlineData("GAZP.RM", "GAZP RM")]
    public void NormalizeSearchKey_ReplacesCommonSeparators(string input, string expected)
    {
        var result = InstrumentQueryHelper.NormalizeSearchKey(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void MatchesLookup_MatchesTickerAfterNormalization()
    {
        var instrument = new InstrumentLookupDto
        {
            Id = Guid.NewGuid(),
            Name = "Gazprom",
            Ticker = "GAZP-RM",
            Isin = null,
            Figi = null,
            CurrencyId = "RUB"
        };
        const string rawKey = "GAZP RM";
        var normalizedKey = InstrumentQueryHelper.NormalizeSearchKey(rawKey);
        var compactKey = normalizedKey.Replace(" ", string.Empty);

        var result = InstrumentQueryHelper.MatchesLookup(instrument, rawKey, normalizedKey, compactKey);

        Assert.True(result);
    }

    [Fact]
    public void MatchesLookup_MatchesNameUsingCompactKey()
    {
        var instrument = new InstrumentLookupDto
        {
            Id = Guid.NewGuid(),
            Name = "T-Bank Holdings",
            Ticker = "T",
            Isin = null,
            Figi = null,
            CurrencyId = "USD"
        };
        const string rawKey = "TBANK";
        var normalizedKey = InstrumentQueryHelper.NormalizeSearchKey(rawKey);
        var compactKey = normalizedKey.Replace(" ", string.Empty);

        var result = InstrumentQueryHelper.MatchesLookup(instrument, rawKey, normalizedKey, compactKey);

        Assert.True(result);
    }
}
