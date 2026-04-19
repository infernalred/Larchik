using Larchik.Application.Portfolios.GetPortfolio;
using Larchik.Application.Portfolios.GetPortfolios;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class PortfolioQueryHandlersTests
{
    [Fact]
    public async Task GetPortfolio_ReturnsOwnedPortfolio()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Main", "USD");
        await harness.Context.SaveChangesAsync();

        var result = await harness.GetHandler.Handle(new GetPortfolioQuery(portfolioId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(portfolioId, result.Value!.Id);
        Assert.Equal("Main", result.Value.Name);
        Assert.Equal("USD", result.Value.ReportingCurrencyId);
    }

    [Fact]
    public async Task GetPortfolio_ReturnsNull_ForForeignPortfolio()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Foreign", userId: PortfolioHandlersTestHarness.OtherUserId);
        await harness.Context.SaveChangesAsync();

        var result = await harness.GetHandler.Handle(new GetPortfolioQuery(portfolioId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPortfolios_ReturnsOnlyCurrentUserPortfolios()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolio1Id = harness.AddPortfolio("Main");
        var portfolio2Id = harness.AddPortfolio("Secondary");
        harness.AddPortfolio("Foreign", userId: PortfolioHandlersTestHarness.OtherUserId);
        await harness.Context.SaveChangesAsync();

        var result = await harness.GetManyHandler.Handle(new GetPortfoliosQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, x => x.Id == portfolio1Id);
        Assert.Contains(result.Value, x => x.Id == portfolio2Id);
        Assert.DoesNotContain(result.Value, x => x.Name == "Foreign");
    }
}
