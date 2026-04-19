using Larchik.Application.Operations.GetOperation;
using Larchik.Application.Operations.GetOperations;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Operations;

public class OperationQueryHandlersTests
{
    [Fact]
    public async Task GetOperation_ReturnsOwnedNonAdministrativeOperation()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        var operationId = harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            instrumentId,
            quantity: 1m,
            price: 100m,
            note: "note");
        await harness.Context.SaveChangesAsync();

        var result = await harness.GetHandler.Handle(new GetOperationQuery(operationId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal("SBER", result.Value!.InstrumentTicker);
        Assert.Equal("note", result.Value.Note);
    }

    [Fact]
    public async Task GetOperation_ReturnsNull_ForAdministrativeOrForeignOperation()
    {
        await using var harness = new OperationsTestHarness();
        var foreignPortfolioId = harness.AddPortfolio("Foreign", userId: OperationsTestHarness.OtherUserId);
        var ownPortfolioId = harness.AddPortfolio("Own");
        var foreignOperationId = harness.AddOperation(foreignPortfolioId, OperationType.Deposit, new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), price: 100m);
        var adminOperationId = harness.AddOperation(ownPortfolioId, OperationType.Split, new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc), quantity: 2m);
        await harness.Context.SaveChangesAsync();

        var foreignResult = await harness.GetHandler.Handle(new GetOperationQuery(foreignOperationId), CancellationToken.None);
        var adminResult = await harness.GetHandler.Handle(new GetOperationQuery(adminOperationId), CancellationToken.None);

        Assert.True(foreignResult.IsSuccess);
        Assert.Null(foreignResult.Value);
        Assert.True(adminResult.IsSuccess);
        Assert.Null(adminResult.Value);
    }

    [Fact]
    public async Task GetOperations_Paginates_AndExcludesAdministrativeOperations()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        harness.AddOperation(portfolioId, OperationType.Deposit, new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), price: 100m);
        harness.AddOperation(portfolioId, OperationType.Split, new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc), quantity: 2m);
        harness.AddOperation(portfolioId, OperationType.Withdraw, new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc), price: 50m);
        harness.AddOperation(portfolioId, OperationType.Fee, new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc), fee: 1m);
        await harness.Context.SaveChangesAsync();

        var result = await harness.GetManyHandler.Handle(new GetOperationsQuery(portfolioId, harness.Page(page: 1, pageSize: 2)), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.All(result.Value.Items, x => Assert.DoesNotContain(x.Type, new[] { OperationType.Split, OperationType.ReverseSplit }));
    }
}
