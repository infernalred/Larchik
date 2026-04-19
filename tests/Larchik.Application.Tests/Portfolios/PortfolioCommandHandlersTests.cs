using Larchik.Application.Portfolios.ClearPortfolioData;
using Larchik.Application.Portfolios.CreatePortfolio;
using Larchik.Application.Portfolios.DeletePortfolio;
using Larchik.Application.Portfolios.EditPortfolio;
using Larchik.Application.Portfolios.RecalculatePortfolio;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class PortfolioCommandHandlersTests
{
    [Fact]
    public async Task Create_CreatesPortfolio_NormalizesNameAndCurrency()
    {
        await using var harness = new PortfolioHandlersTestHarness();

        var result = await harness.CreateHandler.Handle(
            new CreatePortfolioCommand(harness.BuildModel("  Main Portfolio  ", PortfolioHandlersTestHarness.TbankBrokerId, " usd ")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);

        var portfolio = await harness.Context.Portfolios.AsNoTracking().SingleAsync();
        Assert.Equal("Main Portfolio", portfolio.Name);
        Assert.Equal("USD", portfolio.ReportingCurrencyId);
        Assert.Equal(PortfolioHandlersTestHarness.TbankBrokerId, portfolio.BrokerId);
        Assert.Equal(PortfolioHandlersTestHarness.UserId, portfolio.UserId);
    }

    [Fact]
    public async Task Create_Fails_WhenReportingCurrencyIsInvalid()
    {
        await using var harness = new PortfolioHandlersTestHarness();

        var result = await harness.CreateHandler.Handle(
            new CreatePortfolioCommand(harness.BuildModel("Main", PortfolioHandlersTestHarness.TbankBrokerId, " dollars ")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Reporting currency must be a 3-letter code.", result.Error);
    }

    [Fact]
    public async Task Edit_UpdatesPortfolio_NormalizesNameAndCurrency()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Old Name");
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditHandler.Handle(
            new EditPortfolioCommand(
                portfolioId,
                harness.BuildModel("  Updated Name  ", PortfolioHandlersTestHarness.VtbBrokerId, " eur ")),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);

        var portfolio = await harness.Context.Portfolios.AsNoTracking().SingleAsync();
        Assert.Equal("Updated Name", portfolio.Name);
        Assert.Equal(PortfolioHandlersTestHarness.VtbBrokerId, portfolio.BrokerId);
        Assert.Equal("EUR", portfolio.ReportingCurrencyId);
    }

    [Fact]
    public async Task Edit_Fails_WhenBrokerDoesNotExist()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditHandler.Handle(
            new EditPortfolioCommand(
                portfolioId,
                harness.BuildModel("Main", Guid.NewGuid(), "RUB")),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal("Selected broker was not found.", result.Error);
    }

    [Fact]
    public async Task Edit_Fails_WhenReportingCurrencyIsInvalid()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditHandler.Handle(
            new EditPortfolioCommand(
                portfolioId,
                harness.BuildModel("Main", PortfolioHandlersTestHarness.TbankBrokerId, " rubles ")),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal("Reporting currency must be a 3-letter code.", result.Error);
    }

    [Fact]
    public async Task Delete_RemovesOwnedPortfolio()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();

        var result = await harness.DeleteHandler.Handle(new DeletePortfolioCommand(portfolioId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Empty(await harness.Context.Portfolios.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ClearPortfolioData_DeletesOnlySelectedPortfolioData()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var targetPortfolioId = harness.AddPortfolio("Target");
        var otherPortfolioId = harness.AddPortfolio("Other");
        var instrumentId = harness.AddInstrument("SBER");
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

        harness.AddOperation(targetPortfolioId, date);
        harness.AddOperation(otherPortfolioId, date);
        harness.AddPositionSnapshot(targetPortfolioId, instrumentId, date);
        harness.AddPositionSnapshot(otherPortfolioId, instrumentId, date);
        harness.AddPortfolioSnapshot(targetPortfolioId, date);
        harness.AddPortfolioSnapshot(otherPortfolioId, date);
        await harness.Context.SaveChangesAsync();

        var result = await harness.ClearHandler.Handle(new ClearPortfolioDataCommand(targetPortfolioId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, result.Value!.DeletedOperations);
        Assert.Equal(1, result.Value.DeletedPositionSnapshots);
        Assert.Equal(1, result.Value.DeletedPortfolioSnapshots);
        Assert.Single(await harness.Context.Operations.AsNoTracking().ToListAsync());
        Assert.Single(await harness.Context.PositionSnapshots.AsNoTracking().ToListAsync());
        Assert.Single(await harness.Context.PortfolioSnapshots.AsNoTracking().ToListAsync());
        Assert.Equal(otherPortfolioId, (await harness.Context.Operations.AsNoTracking().SingleAsync()).PortfolioId);
    }

    [Fact]
    public async Task RecalculatePortfolio_UsesEarliestOperationDateAndCount()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var date1 = new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddOperation(portfolioId, date1);
        harness.AddOperation(portfolioId, date2);
        await harness.Context.SaveChangesAsync();

        var result = await harness.RecalculateHandler.Handle(new RecalculatePortfolioCommand(portfolioId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(date2, result.Value!.RecalculatedFromDate);
        Assert.Equal(2, result.Value.OperationCount);
        Assert.Single(harness.Recalc.Calls);
        Assert.Equal((portfolioId, date2), harness.Recalc.Calls[0]);
    }

    [Fact]
    public async Task RecalculatePortfolio_ReturnsTodayWhenPortfolioHasNoOperations()
    {
        await using var harness = new PortfolioHandlersTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();

        var utcToday = DateTime.UtcNow.Date;
        var result = await harness.RecalculateHandler.Handle(new RecalculatePortfolioCommand(portfolioId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, result.Value!.OperationCount);
        Assert.Equal(utcToday, result.Value!.RecalculatedFromDate);
        Assert.Empty(harness.Recalc.Calls);
    }
}
