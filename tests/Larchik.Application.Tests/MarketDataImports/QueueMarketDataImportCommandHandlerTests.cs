using Larchik.Application.Contracts;
using Larchik.Application.MarketDataImports.QueueMarketDataImport;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.MarketDataImports;

public sealed class QueueMarketDataImportCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly FromDate = new(2026, 1, 1);

    [Fact]
    public async Task Handle_SkipsRabbitPublish_WhenInstrumentAlreadyExists()
    {
        await using var database = SqliteTestContextFactory.Create();
        database.Context.Instruments.Add(CreateInstrument("RU000A107T19"));
        await database.Context.SaveChangesAsync();

        var result = await CreateHandler(database).Handle(
            new QueueMarketDataImportCommand(PriceSource.MOEX, " ru000a107t19 ", FromDate, "existing-isin"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(MarketDataImportStatus.SkippedExisting, result.Value!.Status);
        Assert.NotNull(result.Value.InstrumentId);
        Assert.Empty(await database.Context.OutboxMessages.ToListAsync());

        var request = await database.Context.MarketDataImportRequests.SingleAsync();
        Assert.Equal("RU000A107T19", request.Isin);
        Assert.Equal(MarketDataImportStatus.SkippedExisting, request.Status);
        Assert.Equal(result.Value.InstrumentId, request.InstrumentId);
    }

    [Fact]
    public async Task Handle_QueuesOutboxMessage_WhenInstrumentDoesNotExist()
    {
        await using var database = SqliteTestContextFactory.Create();

        var result = await CreateHandler(database).Handle(
            new QueueMarketDataImportCommand(PriceSource.MOEX, "ru000a107t19", FromDate, "new-isin"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(MarketDataImportStatus.Queued, result.Value!.Status);
        Assert.Null(result.Value.InstrumentId);

        var request = await database.Context.MarketDataImportRequests.SingleAsync();
        var outbox = await database.Context.OutboxMessages.SingleAsync();
        Assert.Equal(request.Id, result.Value.Id);
        Assert.Contains(request.Id.ToString(), outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Null(outbox.PublishedAt);
    }

    [Fact]
    public async Task Handle_ReturnsExistingRequest_ForRepeatedIdempotencyKey()
    {
        await using var database = SqliteTestContextFactory.Create();
        var handler = CreateHandler(database);
        var command = new QueueMarketDataImportCommand(PriceSource.TBANK, "RU000A107T19", FromDate, "same-key");

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Single(await database.Context.MarketDataImportRequests.ToListAsync());
        Assert.Single(await database.Context.OutboxMessages.ToListAsync());
    }

    private static QueueMarketDataImportCommandHandler CreateHandler(SqliteTestDatabase database) =>
        new(database.Context, new FixedUserAccessor(UserId));

    private static Instrument CreateInstrument(string isin) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Yandex",
        Ticker = "YDEX",
        Isin = isin,
        Type = InstrumentType.Equity,
        CurrencyId = "RUB",
        CategoryId = 4,
        ExchangeId = "MOEX",
        CountryId = "RU",
        IsTrading = true,
        PriceSource = PriceSource.MOEX,
        CreatedBy = UserId,
        UpdatedBy = UserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedUserAccessor(Guid userId) : IUserAccessor
    {
        public Guid GetUserId() => userId;
    }
}
