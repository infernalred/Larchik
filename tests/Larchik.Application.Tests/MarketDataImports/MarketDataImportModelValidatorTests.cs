using Larchik.Application.MarketDataImports.QueueMarketDataImport;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.MarketDataImports;

public sealed class MarketDataImportModelValidatorTests
{
    private readonly MarketDataImportModelValidator validator = new();

    [Fact]
    public async Task Validate_AcceptsValidIsinAndPastDate()
    {
        var result = await validator.ValidateAsync(new MarketDataImportModel(
            PriceSource.MOEX,
            "RU000A107T19",
            new DateOnly(2024, 1, 1)));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("RU000A107T18")]
    [InlineData("NOT-AN-ISIN")]
    [InlineData("")]
    public async Task Validate_RejectsInvalidIsin(string isin)
    {
        var result = await validator.ValidateAsync(new MarketDataImportModel(
            PriceSource.MOEX,
            isin,
            new DateOnly(2024, 1, 1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_RejectsFutureFromDate()
    {
        var result = await validator.ValidateAsync(new MarketDataImportModel(
            PriceSource.TBANK,
            "RU000A107T19",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_RejectsMissingFromDate()
    {
        var result = await validator.ValidateAsync(new MarketDataImportModel(
            PriceSource.MOEX,
            "RU000A107T19",
            default));

        Assert.False(result.IsValid);
    }
}
