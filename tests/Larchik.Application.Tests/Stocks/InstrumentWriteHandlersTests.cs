using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Stocks.CreateStock;
using Larchik.Application.Stocks.EditStock;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Larchik.Application.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Stocks;

public sealed class InstrumentWriteHandlersTests
{
    [Fact]
    public async Task Create_NormalizesInstrumentFields_AndCreatesInitialListing()
    {
        await using var harness = new InstrumentHarness();

        var result = await harness.CreateAsync(new InstrumentModel(
            Name: "  Sberbank  ",
            Ticker: " sber ",
            Isin: "  ru0009029540 ",
            Figi: " bbg004730n88 ",
            Type: InstrumentType.Equity,
            CurrencyId: " rub ",
            CategoryId: 2,
            Exchange: "  moex  ",
            Country: "  ru  ",
            IsTrading: true,
            PriceSource: PriceSource.MOEX));

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);

        var instrument = await harness.Context.Instruments.SingleAsync();
        var listing = await harness.Context.InstrumentListingHistories.SingleAsync();

        Assert.Equal("Sberbank", instrument.Name);
        Assert.Equal("SBER", instrument.Ticker);
        Assert.Equal("RU0009029540", instrument.Isin);
        Assert.Equal("BBG004730N88", instrument.Figi);
        Assert.Equal("RUB", instrument.CurrencyId);
        Assert.Equal("MOEX", instrument.ExchangeId);
        Assert.Equal("RU", instrument.CountryId);

        Assert.Equal(instrument.Id, listing.InstrumentId);
        Assert.Equal("SBER", listing.Ticker);
        Assert.Equal("BBG004730N88", listing.Figi);
        Assert.Equal("RUB", listing.CurrencyId);
        Assert.Equal("MOEX", listing.ExchangeId);
    }

    [Fact]
    public async Task Create_ReturnsFailure_ForDuplicateNormalizedIsin()
    {
        await using var harness = new InstrumentHarness();
        harness.AddInstrument("SBER", "RU0009029540", "BBG004730N88", "RUB", "MOEX");
        await harness.Context.SaveChangesAsync();

        var result = await harness.CreateAsync(new InstrumentModel(
            Name: "Another",
            Ticker: "SBER2",
            Isin: "  ru0009029540 ",
            Figi: "BBG000000002",
            Type: InstrumentType.Equity,
            CurrencyId: "rub",
            CategoryId: 2,
            Exchange: "moex",
            Country: "RU",
            IsTrading: true,
            PriceSource: PriceSource.MOEX));

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal("An instrument with the same ISIN already exists.", result.Error);
    }

    [Fact]
    public async Task Edit_DoesNotCreateNewListing_WhenListingFieldsAreEquivalentAfterNormalization()
    {
        await using var harness = new InstrumentHarness();
        var instrumentId = harness.AddInstrument("SBER", "RU0009029540", "BBG004730N88", "RUB", "MOEX");
        harness.AddListing(instrumentId, "SBER", "BBG004730N88", "RUB", "MOEX", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        await harness.Context.SaveChangesAsync();

        var result = await harness.EditAsync(
            instrumentId,
            new InstrumentModel(
                Name: "  Sberbank PJSC  ",
                Ticker: " sber ",
                Isin: " ru0009029540 ",
                Figi: " bbg004730n88 ",
                Type: InstrumentType.Equity,
                CurrencyId: " rub ",
                CategoryId: 2,
                Exchange: "  moex ",
                Country: "  RU ",
                IsTrading: true,
                PriceSource: PriceSource.MOEX));

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);

        var listings = await harness.Context.InstrumentListingHistories
            .Where(x => x.InstrumentId == instrumentId)
            .OrderBy(x => x.EffectiveFrom)
            .ToListAsync();
        var instrument = await harness.Context.Instruments.SingleAsync(x => x.Id == instrumentId);

        Assert.Single(listings);
        Assert.Equal("SBER", instrument.Ticker);
        Assert.Equal("BBG004730N88", instrument.Figi);
        Assert.Equal("RUB", instrument.CurrencyId);
        Assert.Equal("MOEX", instrument.ExchangeId);
        Assert.Equal("Sberbank PJSC", instrument.Name);
        Assert.Equal("RU", instrument.CountryId);
    }

    private sealed class InstrumentHarness : IAsyncDisposable
    {
        private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private readonly SqliteTestDatabase database;
        public LarchikContext Context { get; }

        public InstrumentHarness()
        {
            database = SqliteTestContextFactory.Create();
            Context = database.Context;
            SeedCurrencies();
        }

        public Task<Result<Unit>?> CreateAsync(InstrumentModel model) =>
            new CreateInstrumentCommandHandler(Context, new FixedUserAccessor(UserId))
                .Handle(new CreateInstrumentCommand(model), CancellationToken.None);

        public Task<Result<Unit>?> EditAsync(Guid id, InstrumentModel model) =>
            new EditInstrumentCommandHandler(Context, new FixedUserAccessor(UserId))
                .Handle(new EditInstrumentCommand(id, model), CancellationToken.None);

        public Guid AddInstrument(string ticker, string? isin, string? figi, string currencyId, string? exchange)
        {
            var id = Guid.NewGuid();
            Context.Instruments.Add(new Instrument
            {
                Id = id,
                Name = ticker,
                Ticker = ticker,
                Isin = isin,
                Figi = figi,
                Type = InstrumentType.Equity,
                CurrencyId = currencyId,
                CategoryId = 2,
                ExchangeId = exchange,
                CountryId = "RU",
                IsTrading = true,
                PriceSource = PriceSource.MOEX,
                CreatedBy = UserId,
                UpdatedBy = UserId,
                CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            return id;
        }

        public void AddListing(Guid instrumentId, string ticker, string? figi, string currencyId, string? exchange, DateTime effectiveFrom)
        {
            Context.InstrumentListingHistories.Add(new InstrumentListingHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Ticker = ticker,
                Figi = figi,
                CurrencyId = currencyId,
                ExchangeId = exchange,
                EffectiveFrom = effectiveFrom,
                CreatedAt = effectiveFrom,
                UpdatedAt = effectiveFrom
            });
        }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }

        private void SeedCurrencies()
        {
            if (Context.Currencies.Any())
            {
                return;
            }

            Context.Currencies.AddRange(
                new Currency { Id = "RUB" },
                new Currency { Id = "USD" });
            Context.SaveChanges();
        }
    }

    private sealed class FixedUserAccessor(Guid userId) : IUserAccessor
    {
        public Guid GetUserId() => userId;
    }
}
