using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Tests.Prices;

internal sealed class PriceSyncTestHarness : IAsyncDisposable
{
    internal static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly DateTime SeedTimestamp = new(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection connection;

    public PriceSyncTestHarness()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<LarchikContext>()
            .UseSqlite(connection)
            .Options;

        Context = new LarchikContext(options);
        Context.Database.EnsureCreated();
        SeedReferenceData();
    }

    public LarchikContext Context { get; }

    public Guid AddInstrument(
        string ticker,
        string currencyId = "RUB",
        string? figi = null,
        InstrumentType type = InstrumentType.Equity,
        PriceSource? priceSource = null,
        bool isTrading = true,
        string? country = null,
        string? exchange = null)
    {
        var instrumentId = Guid.NewGuid();
        Context.Instruments.Add(new Instrument
        {
            Id = instrumentId,
            Name = ticker,
            Ticker = ticker,
            Isin = $"{ticker}0000001",
            Figi = figi,
            Type = type,
            CurrencyId = currencyId,
            CategoryId = 1,
            Exchange = exchange,
            Country = country,
            IsTrading = isTrading,
            PriceSource = priceSource,
            CreatedBy = UserId,
            UpdatedBy = UserId,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });

        return instrumentId;
    }

    public void AddListingHistory(
        Guid instrumentId,
        string ticker,
        string currencyId,
        DateTime effectiveFrom,
        DateTime? effectiveTo = null,
        string? figi = null,
        string? exchange = null)
    {
        Context.InstrumentListingHistories.Add(new InstrumentListingHistory
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            Ticker = ticker,
            CurrencyId = currencyId,
            Figi = figi,
            Exchange = exchange,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });
    }

    public void AddInstrumentAlias(Guid instrumentId, string aliasCode)
    {
        Context.InstrumentAliases.Add(new InstrumentAlias
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            AliasCode = aliasCode,
            NormalizedAliasCode = aliasCode.Trim().ToUpperInvariant()
        });
    }

    public void AddFxRate(string baseCurrencyId, string quoteCurrencyId, DateTime date, decimal rate, string source = "TEST")
    {
        Context.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(),
            BaseCurrencyId = baseCurrencyId,
            QuoteCurrencyId = quoteCurrencyId,
            Date = date,
            Rate = rate,
            Source = source,
            CreatedAt = SeedTimestamp
        });
    }

    public void AddPrice(Guid instrumentId, DateTime date, decimal value, string currencyId, string provider, string? sourceCurrencyId = null)
    {
        Context.Prices.Add(new Price
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            Date = date,
            Value = value,
            CurrencyId = currencyId,
            SourceCurrencyId = sourceCurrencyId,
            Provider = provider,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }

    private void SeedReferenceData()
    {
        Context.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "price-test-user",
            NormalizedUserName = "PRICE-TEST-USER"
        });

        Context.SaveChanges();
    }
}
