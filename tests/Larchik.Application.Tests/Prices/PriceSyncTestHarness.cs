using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Larchik.Application.Tests.TestInfrastructure;

namespace Larchik.Application.Tests.Prices;

internal sealed class PriceSyncTestHarness : IAsyncDisposable
{
    internal static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid PortfolioId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid BrokerId = Guid.Parse("f6f784ea-b520-4bc5-8a32-9a17f1637003");
    internal static readonly DateTime SeedTimestamp = new(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDatabase database;

    public PriceSyncTestHarness()
    {
        database = SqliteTestContextFactory.Create();
        Context = database.Context;
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
            ExchangeId = exchange,
            CountryId = country,
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
            ExchangeId = exchange,
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
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
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

    public void AddOperation(
        Guid instrumentId,
        OperationType type = OperationType.Buy,
        decimal quantity = 1m,
        DateTime? tradeDate = null,
        string currencyId = "USD",
        decimal price = 1m)
    {
        Context.Operations.Add(new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = PortfolioId,
            InstrumentId = instrumentId,
            Type = type,
            Quantity = quantity,
            Price = price,
            Fee = 0m,
            CurrencyId = currencyId,
            TradeDate = tradeDate ?? SeedTimestamp,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }

    private void SeedReferenceData()
    {
        Context.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "price-test-user",
            NormalizedUserName = "PRICE-TEST-USER"
        });
        Context.Portfolios.Add(new Portfolio
        {
            Id = PortfolioId,
            UserId = UserId,
            BrokerId = BrokerId,
            Name = "Price sync test portfolio",
            ReportingCurrencyId = "RUB",
            CreatedAt = SeedTimestamp
        });

        Context.SaveChanges();
    }
}
