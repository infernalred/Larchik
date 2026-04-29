using Larchik.Application.Operations.CreateOperation;
using Larchik.Application.Operations.DeleteOperation;
using Larchik.Application.Operations.EditOperation;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Operations;

public class OperationCommandHandlersTests
{
    [Fact]
    public async Task Create_CreatesOperation_NormalizesData_AndSchedulesRebuild()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        await harness.Context.SaveChangesAsync();

        var tradeDate = new DateTimeOffset(2026, 4, 20, 10, 0, 0, TimeSpan.Zero);
        var result = await harness.CreateHandler.Handle(
            new CreateOperationCommand(
                portfolioId,
                harness.BuildModel(instrumentId, OperationType.Buy, 10m, 100m, 1m, " rub ", tradeDate, note: "  test note  ")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);

        var operation = await harness.Context.Operations.SingleAsync();
        Assert.Equal(portfolioId, operation.PortfolioId);
        Assert.Equal(instrumentId, operation.InstrumentId);
        Assert.Equal("RUB", operation.CurrencyId);
        Assert.Equal("test note", operation.Note);
        Assert.True(operation.BrokerOperationKey.StartsWith("manual:v2:", StringComparison.Ordinal) ||
                    operation.BrokerOperationKey.StartsWith("manual:v3:", StringComparison.Ordinal));
        Assert.Single(harness.Recalc.Calls);
        Assert.Equal((portfolioId, tradeDate.UtcDateTime), harness.Recalc.Calls[0]);
    }

    [Fact]
    public async Task Create_IgnoresInstrumentForCashOperation()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        await harness.Context.SaveChangesAsync();

        var result = await harness.CreateHandler.Handle(
            new CreateOperationCommand(
                portfolioId,
                harness.BuildModel(instrumentId, OperationType.Deposit, 0m, 1000m, 0m, "RUB", new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var operation = await harness.Context.Operations.SingleAsync();
        Assert.Null(operation.InstrumentId);
    }

    [Fact]
    public async Task Create_Fails_WhenInstrumentIsRequiredButMissing()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();

        var result = await harness.CreateHandler.Handle(
            new CreateOperationCommand(
                portfolioId,
                harness.BuildModel(null, OperationType.Buy, 1m, 100m, 0m, "RUB", new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Instrument is required for selected operation type.", result.Error);
    }

    [Fact]
    public async Task Edit_UpdatesOperation_AndSchedulesRebuildFromEarliestDate()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        var operationId = harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            instrumentId,
            quantity: 1m,
            price: 100m,
            currencyId: "RUB",
            brokerOperationKey: "manual:v2:seed:000001");
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditHandler.Handle(
            new EditOperationCommand(
                operationId,
                harness.BuildModel(
                    instrumentId,
                    OperationType.Buy,
                    2m,
                    110m,
                    1m,
                    "USD",
                    new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
                    note: "edited")),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);

        var operation = await harness.Context.Operations.SingleAsync();
        Assert.Equal(2m, operation.Quantity);
        Assert.Equal(110m, operation.Price);
        Assert.Equal(1m, operation.Fee);
        Assert.Equal("USD", operation.CurrencyId);
        Assert.Equal("edited", operation.Note);
        Assert.Single(harness.Recalc.Calls);
        Assert.Equal((portfolioId, new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)), harness.Recalc.Calls[0]);
    }

    [Fact]
    public async Task Edit_ReturnsFailure_ForAdministrativeRequestType()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var operationId = harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            quantity: 1m,
            price: 100m);
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditHandler.Handle(
            new EditOperationCommand(
                operationId,
                harness.BuildModel(null, OperationType.Split, 2m, 0m, 0m, "RUB", new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal("Split and reverse split must be managed as administrative corporate actions.", result.Error);
    }

    [Fact]
    public async Task Edit_IgnoresInstrumentForCashOperation_AndNormalizesData()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        var operationId = harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            instrumentId,
            quantity: 1m,
            price: 100m,
            currencyId: "RUB",
            brokerOperationKey: "manual:v2:seed:000001");
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditHandler.Handle(
            new EditOperationCommand(
                operationId,
                harness.BuildModel(
                    instrumentId,
                    OperationType.Deposit,
                    0m,
                    1500m,
                    0m,
                    " usd ",
                    new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
                    note: "  edited cash  ")),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);

        var operation = await harness.Context.Operations.SingleAsync();
        Assert.Null(operation.InstrumentId);
        Assert.Equal("USD", operation.CurrencyId);
        Assert.Equal("edited cash", operation.Note);
    }

    [Fact]
    public async Task Edit_Fails_WhenCurrencyIsInvalid()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var operationId = harness.AddOperation(
            portfolioId,
            OperationType.Deposit,
            new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            price: 100m,
            currencyId: "RUB");
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditHandler.Handle(
            new EditOperationCommand(
                operationId,
                harness.BuildModel(
                    null,
                    OperationType.Deposit,
                    0m,
                    100m,
                    0m,
                    " rubles ",
                    new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal("Currency must be a 3-letter code.", result.Error);
    }

    [Fact]
    public async Task Delete_RemovesOperation_AndSchedulesRebuild()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var operationId = harness.AddOperation(portfolioId, OperationType.Deposit, tradeDate, price: 1000m);
        await harness.Context.SaveChangesAsync();

        var result = await harness.DeleteHandler.Handle(new DeleteOperationCommand(operationId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Empty(await harness.Context.Operations.ToListAsync());
        Assert.Single(harness.Recalc.Calls);
        Assert.Equal((portfolioId, tradeDate), harness.Recalc.Calls[0]);
    }

    [Fact]
    public async Task Delete_FailsForAdministrativeOperation()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var operationId = harness.AddOperation(portfolioId, OperationType.Split, new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), quantity: 2m);
        await harness.Context.SaveChangesAsync();

        var result = await harness.DeleteHandler.Handle(new DeleteOperationCommand(operationId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Split and reverse split must be managed as administrative corporate actions.", result.Error);
    }

    [Fact]
    public async Task Delete_RestoresOperation_WhenRebuildSchedulingFails()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var operationId = harness.AddOperation(portfolioId, OperationType.Deposit, tradeDate, price: 1000m);
        await harness.Context.SaveChangesAsync();

        var handler = new DeleteOperationCommandHandler(
            harness.Context,
            new OperationsTestHarness.FixedUserAccessor(OperationsTestHarness.UserId),
            new FailingRecalcService());
        var result = await handler.Handle(new DeleteOperationCommand(operationId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Operation delete rolled back because portfolio rebuild scheduling failed.", result.Error);
        Assert.NotNull(await harness.Context.Operations.FirstOrDefaultAsync(x => x.Id == operationId));
    }

    private sealed class FailingRecalcService : Larchik.Application.Contracts.IPortfolioRecalcService
    {
        public Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
