using Larchik.Application.Contracts;
using Larchik.Application.Models;
using Larchik.Application.Stocks.InstrumentCorporateActions.CreateInstrumentCorporateAction;
using Larchik.Application.Stocks.InstrumentCorporateActions.DeleteInstrumentCorporateAction;
using Larchik.Application.Stocks.InstrumentCorporateActions.EditInstrumentCorporateAction;
using Larchik.Application.Stocks.InstrumentCorporateActions.GetInstrumentCorporateActions;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Stocks.InstrumentCorporateActions;

public sealed class InstrumentCorporateActionsCrudHandlersTests
{
    [Fact]
    public async Task Create_ThenList_ReturnsOnlySupportedTypesInDescendingDateOrder()
    {
        await using var db = SqliteTestContextFactory.Create();
        var context = db.Context;
        var recalc = new RecordingPortfolioRecalcService();
        var instrumentId = await SeedInstrumentAsync(context, InstrumentType.Equity);

        var createHandler = new CreateInstrumentCorporateActionCommandHandler(context, recalc);
        var listHandler = new GetInstrumentCorporateActionsQueryHandler(context);

        var older = await createHandler.Handle(
            new CreateInstrumentCorporateActionCommand(
                instrumentId,
                new InstrumentCorporateActionModel(
                    OperationType.Split,
                    2m,
                    UtcDate(2026, 4, 20),
                    "older split")),
            CancellationToken.None);
        var newer = await createHandler.Handle(
            new CreateInstrumentCorporateActionCommand(
                instrumentId,
                new InstrumentCorporateActionModel(
                    OperationType.ReverseSplit,
                    0.5m,
                    UtcDate(2026, 4, 21),
                    "newer reverse split")),
            CancellationToken.None);

        Assert.True(older.IsSuccess);
        Assert.True(newer.IsSuccess);

        context.InstrumentCorporateActions.Add(new InstrumentCorporateAction
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            Type = OperationType.Buy,
            Factor = 1m,
            EffectiveDate = new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
            Note = "legacy noise"
        });
        await context.SaveChangesAsync();

        var list = await listHandler.Handle(new GetInstrumentCorporateActionsQuery(instrumentId), CancellationToken.None);

        Assert.True(list.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<InstrumentCorporateActionDto>>(list.Value);
        Assert.Equal(2, items.Count);
        Assert.Collection(
            items,
            first =>
            {
                Assert.Equal(OperationType.ReverseSplit, first.Type);
                Assert.Equal(new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc), first.EffectiveDate);
            },
            second =>
            {
                Assert.Equal(OperationType.Split, second.Type);
                Assert.Equal(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), second.EffectiveDate);
            });
    }

    [Fact]
    public async Task Create_ReturnsFailure_ForUnsupportedInstrumentType()
    {
        await using var db = SqliteTestContextFactory.Create();
        var context = db.Context;
        var recalc = new RecordingPortfolioRecalcService();
        var instrumentId = await SeedInstrumentAsync(context, InstrumentType.Currency);
        var handler = new CreateInstrumentCorporateActionCommandHandler(context, recalc);

        var result = await handler.Handle(
            new CreateInstrumentCorporateActionCommand(
                instrumentId,
                new InstrumentCorporateActionModel(
                    OperationType.Split,
                    2m,
                    UtcDate(2026, 4, 20),
                    "currency split")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Corporate actions are supported only for Equity and Etf instruments.", result.Error);
    }

    [Fact]
    public async Task Edit_ReturnsFailure_ForInvalidFactorByType()
    {
        await using var db = SqliteTestContextFactory.Create();
        var context = db.Context;
        var recalc = new RecordingPortfolioRecalcService();
        var instrumentId = await SeedInstrumentAsync(context, InstrumentType.Equity);
        var createHandler = new CreateInstrumentCorporateActionCommandHandler(context, recalc);
        var editHandler = new EditInstrumentCorporateActionCommandHandler(context, recalc);

        var created = await createHandler.Handle(
            new CreateInstrumentCorporateActionCommand(
                instrumentId,
                new InstrumentCorporateActionModel(
                    OperationType.Split,
                    2m,
                    UtcDate(2026, 4, 20),
                    "valid split")),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        var edited = await editHandler.Handle(
            new EditInstrumentCorporateActionCommand(
                instrumentId,
                created.Value,
                new InstrumentCorporateActionModel(
                    OperationType.ReverseSplit,
                    2m,
                    UtcDate(2026, 4, 20),
                    "invalid reverse split factor")),
            CancellationToken.None);

        Assert.NotNull(edited);
        Assert.False(edited!.IsSuccess);
        Assert.Equal("Reverse split factor must be greater than 0 and less than 1.", edited.Error);
    }

    [Fact]
    public async Task Delete_RemovesCorporateAction()
    {
        await using var db = SqliteTestContextFactory.Create();
        var context = db.Context;
        var recalc = new RecordingPortfolioRecalcService();
        var instrumentId = await SeedInstrumentAsync(context, InstrumentType.Equity);
        var createHandler = new CreateInstrumentCorporateActionCommandHandler(context, recalc);
        var deleteHandler = new DeleteInstrumentCorporateActionCommandHandler(context, recalc);
        var listHandler = new GetInstrumentCorporateActionsQueryHandler(context);

        var created = await createHandler.Handle(
            new CreateInstrumentCorporateActionCommand(
                instrumentId,
                new InstrumentCorporateActionModel(
                    OperationType.Split,
                    3m,
                    UtcDate(2026, 4, 20),
                    "to delete")),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        var deleted = await deleteHandler.Handle(
            new DeleteInstrumentCorporateActionCommand(instrumentId, created.Value),
            CancellationToken.None);
        Assert.True(deleted.IsSuccess);

        var listed = await listHandler.Handle(new GetInstrumentCorporateActionsQuery(instrumentId), CancellationToken.None);
        Assert.True(listed.IsSuccess);
        Assert.Empty(listed.Value!);
    }

    [Theory]
    [InlineData(20, 25, 20)]
    [InlineData(20, 15, 15)]
    public async Task Edit_SchedulesRebuild_FromMinimumOfOldAndNewEffectiveDate(
        int originalDay,
        int updatedDay,
        int expectedRebuildDay)
    {
        await using var db = SqliteTestContextFactory.Create();
        var context = db.Context;
        var recalc = new RecordingPortfolioRecalcService();
        var instrumentId = await SeedInstrumentAsync(context, InstrumentType.Equity);
        var portfolioId = await SeedPortfolioAndOperationAsync(context, instrumentId);
        var createHandler = new CreateInstrumentCorporateActionCommandHandler(context, recalc);
        var editHandler = new EditInstrumentCorporateActionCommandHandler(context, recalc);

        var created = await createHandler.Handle(
            new CreateInstrumentCorporateActionCommand(
                instrumentId,
                new InstrumentCorporateActionModel(
                    OperationType.Split,
                    2m,
                    UtcDate(2026, 4, originalDay),
                    "original action")),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        var edited = await editHandler.Handle(
            new EditInstrumentCorporateActionCommand(
                instrumentId,
                created.Value,
                new InstrumentCorporateActionModel(
                    OperationType.Split,
                    3m,
                    UtcDate(2026, 4, updatedDay),
                    "updated action")),
            CancellationToken.None);
        Assert.NotNull(edited);
        Assert.True(edited!.IsSuccess);

        var expectedFromDate = new DateTime(2026, 4, expectedRebuildDay, 0, 0, 0, DateTimeKind.Utc);
        Assert.Contains(recalc.Calls, x => x.PortfolioId == portfolioId && x.FromDate == expectedFromDate);
    }

    private static async Task<Guid> SeedInstrumentAsync(LarchikContext context, InstrumentType type)
    {
        const string currencyId = "USD";
        const int categoryId = 1001;

        if (!await context.Currencies.AnyAsync(x => x.Id == currencyId))
        {
            context.Currencies.Add(new Currency { Id = currencyId });
        }

        if (!await context.Categories.AnyAsync(x => x.Id == categoryId))
        {
            context.Categories.Add(new Category { Id = categoryId, Name = "Test" });
        }

        var instrumentId = Guid.NewGuid();
        context.Instruments.Add(new Instrument
        {
            Id = instrumentId,
            Name = "Instrument",
            Ticker = $"T{Guid.NewGuid():N}"[..16],
            Type = type,
            CurrencyId = currencyId,
            CategoryId = categoryId,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });

        await context.SaveChangesAsync();
        return instrumentId;
    }

    private static DateTimeOffset UtcDate(int year, int month, int day) =>
        new(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));

    private static async Task<Guid> SeedPortfolioAndOperationAsync(LarchikContext context, Guid instrumentId)
    {
        var userId = Guid.NewGuid();
        var brokerId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var now = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

        context.Users.Add(new AppUser
        {
            Id = userId,
            UserName = $"u{Guid.NewGuid():N}",
            Email = $"u{Guid.NewGuid():N}@example.com"
        });
        context.Brokers.Add(new Broker
        {
            Id = brokerId,
            Name = "Test broker",
            Code = $"BRK{Guid.NewGuid():N}"[..16]
        });
        context.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            UserId = userId,
            BrokerId = brokerId,
            Name = "Test portfolio",
            ReportingCurrencyId = "USD",
            CreatedAt = now
        });
        context.Operations.Add(new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            InstrumentId = instrumentId,
            Type = OperationType.Buy,
            Quantity = 1m,
            Price = 100m,
            Fee = 0m,
            CurrencyId = "USD",
            TradeDate = now,
            SettlementDate = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return portfolioId;
    }

    private sealed class RecordingPortfolioRecalcService : IPortfolioRecalcService
    {
        public List<RecalcCall> Calls { get; } = [];

        public Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default)
        {
            Calls.Add(new RecalcCall(portfolioId, fromDate));
            return Task.CompletedTask;
        }
    }

    private sealed record RecalcCall(Guid PortfolioId, DateTime FromDate);
}
