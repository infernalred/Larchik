using Larchik.Application.MarketDataImports.Processing;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Larchik.Application.Tests.MarketDataImports;

public sealed class ProcessMarketDataImportCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime ImportDate = new(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_RechecksInstrumentAndSkipsSource_WhenInstrumentAppearedAfterQueueing()
    {
        await using var database = SqliteTestContextFactory.Create();
        var request = AddRequest(database, "RU000A107T19");
        database.Context.Instruments.Add(CreateInstrument(request.Isin));
        await database.Context.SaveChangesAsync();
        var source = new FakeMarketDataImportSource(PriceSource.MOEX);

        var result = await CreateHandler(database, source).Handle(request.Id, CancellationToken.None);

        Assert.Equal(MarketDataImportProcessOutcome.Completed, result.Outcome);
        Assert.Equal(0, source.ResolveCalls);
        Assert.Equal(0, source.LoadCalls);
        var saved = await database.Context.MarketDataImportRequests.SingleAsync();
        Assert.Equal(MarketDataImportStatus.SkippedExisting, saved.Status);
        Assert.NotNull(saved.InstrumentId);
    }

    [Fact]
    public async Task Handle_CreatesInstrumentAndStoresPrices_ForMissingIsin()
    {
        await using var database = SqliteTestContextFactory.Create();
        var request = AddRequest(database, "RU000A107T19");
        await database.Context.SaveChangesAsync();
        var source = new FakeMarketDataImportSource(PriceSource.MOEX)
        {
            Resolved = new ResolvedMarketDataInstrument(
                Name: "МКПАО ЯНДЕКС",
                Ticker: "YDEX",
                Isin: request.Isin,
                Figi: null,
                Type: InstrumentType.Equity,
                CurrencyId: "RUB",
                ExchangeId: "MOEX",
                CountryId: "RU",
                IsTrading: true,
                SourceInstrumentCode: "YDEX",
                Board: "TQBR",
                Engine: "stock",
                Market: "shares",
                ListedFrom: new DateOnly(2024, 7, 8)),
            Prices =
            [
                new MarketDataImportPricePoint(new DateOnly(2026, 8, 14), 404.5m, "RUB", "RUB")
            ]
        };

        var result = await CreateHandler(database, source).Handle(request.Id, CancellationToken.None);

        Assert.Equal(MarketDataImportProcessOutcome.Completed, result.Outcome);
        Assert.Equal(1, source.ResolveCalls);
        Assert.Equal(1, source.LoadCalls);

        var instrument = await database.Context.Instruments.SingleAsync();
        var listing = await database.Context.InstrumentListingHistories.SingleAsync();
        var price = await database.Context.Prices.SingleAsync();
        var saved = await database.Context.MarketDataImportRequests.SingleAsync();

        Assert.Equal("RU000A107T19", instrument.Isin);
        Assert.Equal("YDEX", instrument.Ticker);
        Assert.Equal(PriceSource.MOEX, instrument.PriceSource);
        Assert.Equal(instrument.Id, listing.InstrumentId);
        Assert.Equal(instrument.Id, price.InstrumentId);
        Assert.Equal(404.5m, price.Value);
        Assert.Equal("MOEX", price.Provider);
        Assert.Equal(MarketDataImportStatus.Succeeded, saved.Status);
        Assert.Equal(instrument.Id, saved.InstrumentId);
        Assert.Equal(1, saved.InsertedPrices);
    }

    [Fact]
    public async Task Handle_ContinuesWithOutboxUntilEveryDateChunkIsProcessed()
    {
        await using var database = SqliteTestContextFactory.Create();
        var request = AddRequest(database, "RU000A107T19");
        request.FromDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        request.NextDate = request.FromDate;
        await database.Context.SaveChangesAsync();
        var source = new FakeMarketDataImportSource(PriceSource.MOEX)
        {
            Resolved = CreateResolved(request.Isin)
        };
        var handler = CreateHandler(database, source, new MarketDataImportOptions { ChunkDays = 30 });

        var first = await handler.Handle(request.Id, CancellationToken.None);
        var second = await handler.Handle(request.Id, CancellationToken.None);

        Assert.Equal(MarketDataImportProcessOutcome.Continue, first.Outcome);
        Assert.Equal(MarketDataImportProcessOutcome.Completed, second.Outcome);
        Assert.Equal(1, source.ResolveCalls);
        Assert.Equal(2, source.LoadCalls);
        Assert.Collection(
            source.PriceRequests,
            x =>
            {
                Assert.Equal(new DateOnly(2026, 7, 1), x.FromDate);
                Assert.Equal(new DateOnly(2026, 7, 30), x.ToDate);
            },
            x =>
            {
                Assert.Equal(new DateOnly(2026, 7, 31), x.FromDate);
                Assert.Equal(new DateOnly(2026, 8, 14), x.ToDate);
            });
        Assert.Single(await database.Context.OutboxMessages.ToListAsync());
        Assert.Equal(MarketDataImportStatus.Succeeded, (await database.Context.MarketDataImportRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Handle_RetriesTransientSourceFailureAndEventuallyFails()
    {
        await using var database = SqliteTestContextFactory.Create();
        var request = AddRequest(database, "RU000A107T19");
        await database.Context.SaveChangesAsync();
        var source = new FakeMarketDataImportSource(PriceSource.MOEX)
        {
            ResolveFailure = MarketDataSourceResult<ResolvedMarketDataInstrument>.TransientFailure("temporary outage")
        };
        var handler = CreateHandler(database, source, new MarketDataImportOptions { MaxAttempts = 2 });

        var first = await handler.Handle(request.Id, CancellationToken.None);
        var second = await handler.Handle(request.Id, CancellationToken.None);

        Assert.Equal(MarketDataImportProcessOutcome.Retry, first.Outcome);
        Assert.Equal(MarketDataImportProcessOutcome.Failed, second.Outcome);
        var saved = await database.Context.MarketDataImportRequests.SingleAsync();
        Assert.Equal(MarketDataImportStatus.Failed, saved.Status);
        Assert.Equal(2, saved.Attempt);
        Assert.Equal("temporary outage", saved.LastError);
        Assert.Equal(0, source.LoadCalls);
    }

    private static ProcessMarketDataImportCommandHandler CreateHandler(
        SqliteTestDatabase database,
        IMarketDataImportSource source,
        MarketDataImportOptions? options = null) =>
        new(
            database.Context,
            [source],
            Options.Create(options ?? new MarketDataImportOptions { ChunkDays = 30 }),
            NullLogger<ProcessMarketDataImportCommandHandler>.Instance);

    private static ResolvedMarketDataInstrument CreateResolved(string isin) => new(
        Name: "МКПАО ЯНДЕКС",
        Ticker: "YDEX",
        Isin: isin,
        Figi: null,
        Type: InstrumentType.Equity,
        CurrencyId: "RUB",
        ExchangeId: "MOEX",
        CountryId: "RU",
        IsTrading: true,
        SourceInstrumentCode: "YDEX",
        Board: "TQBR",
        Engine: "stock",
        Market: "shares",
        ListedFrom: new DateOnly(2024, 7, 8));

    private static MarketDataImportRequest AddRequest(SqliteTestDatabase database, string isin)
    {
        var request = new MarketDataImportRequest
        {
            Id = Guid.NewGuid(),
            RequestedBy = UserId,
            Source = PriceSource.MOEX,
            Isin = isin,
            FromDate = ImportDate,
            ToDate = ImportDate,
            NextDate = ImportDate,
            Status = MarketDataImportStatus.Queued,
            CreatedAt = ImportDate,
            UpdatedAt = ImportDate
        };
        database.Context.MarketDataImportRequests.Add(request);
        return request;
    }

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
        CreatedAt = ImportDate,
        UpdatedAt = ImportDate
    };

    private sealed class FakeMarketDataImportSource(PriceSource source) : IMarketDataImportSource
    {
        public PriceSource Source => source;
        public int ResolveCalls { get; private set; }
        public int LoadCalls { get; private set; }
        public ResolvedMarketDataInstrument? Resolved { get; init; }
        public IReadOnlyCollection<MarketDataImportPricePoint> Prices { get; init; } = [];
        public MarketDataSourceResult<ResolvedMarketDataInstrument>? ResolveFailure { get; init; }
        public List<MarketDataImportPriceLoadRequest> PriceRequests { get; } = [];

        public Task<MarketDataSourceResult<ResolvedMarketDataInstrument>> ResolveAsync(
            string isin,
            CancellationToken cancellationToken)
        {
            ResolveCalls++;
            if (ResolveFailure is not null)
            {
                return Task.FromResult(ResolveFailure);
            }

            return Task.FromResult(Resolved is null
                ? MarketDataSourceResult<ResolvedMarketDataInstrument>.PermanentFailure("not configured")
                : MarketDataSourceResult<ResolvedMarketDataInstrument>.Success(Resolved));
        }

        public Task<MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>> LoadPricesAsync(
            MarketDataImportPriceLoadRequest request,
            CancellationToken cancellationToken)
        {
            LoadCalls++;
            PriceRequests.Add(request);
            return Task.FromResult(MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.Success(Prices));
        }
    }
}
