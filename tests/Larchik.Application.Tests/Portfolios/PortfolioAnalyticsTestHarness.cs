using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.GetAggregatePortfolioPerformance;
using Larchik.Application.Portfolios.GetAggregatePortfolioSummary;
using Larchik.Application.Portfolios.GetPortfolioPerformance;
using Larchik.Application.Portfolios.GetPortfolioSummary;
using Larchik.Application.Portfolios.GetPortfoliosSummary;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Larchik.Application.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

internal sealed class PortfolioAnalyticsTestHarness : IAsyncDisposable
{
    internal static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid BrokerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly DateTime SeedTimestamp = new(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDatabase database;
    private readonly LarchikContext context;
    private readonly GetPortfoliosSummaryQueryHandler portfoliosSummaryHandler;
    private readonly GetPortfolioSummaryQueryHandler portfolioSummaryHandler;
    private readonly GetAggregatePortfolioSummaryQueryHandler aggregateSummaryHandler;
    private readonly GetPortfolioPerformanceQueryHandler portfolioPerformanceHandler;
    private readonly GetAggregatePortfolioPerformanceQueryHandler aggregatePerformanceHandler;

    public PortfolioAnalyticsTestHarness()
    {
        database = SqliteTestContextFactory.Create();
        context = database.Context;

        SeedReferenceData();

        var userAccessor = new FixedUserAccessor(UserId);
        portfoliosSummaryHandler = new GetPortfoliosSummaryQueryHandler(context, userAccessor);
        portfolioSummaryHandler = new GetPortfolioSummaryQueryHandler(context, userAccessor);
        aggregateSummaryHandler = new GetAggregatePortfolioSummaryQueryHandler(context, userAccessor);
        portfolioPerformanceHandler = new GetPortfolioPerformanceQueryHandler(context, userAccessor);
        aggregatePerformanceHandler = new GetAggregatePortfolioPerformanceQueryHandler(context, userAccessor);
    }

    public Guid AddPortfolio(string name, string reportingCurrencyId, Guid? userId = null)
    {
        var portfolioId = Guid.NewGuid();
        context.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            UserId = userId ?? UserId,
            BrokerId = BrokerId,
            Name = name,
            ReportingCurrencyId = reportingCurrencyId,
            CreatedAt = SeedTimestamp
        });

        return portfolioId;
    }

    public Guid AddInstrument(string ticker, string currencyId)
    {
        var instrumentId = Guid.NewGuid();
        context.Instruments.Add(new Instrument
        {
            Id = instrumentId,
            Name = ticker,
            Ticker = ticker,
            Isin = $"{ticker}0000001",
            Type = InstrumentType.Equity,
            CurrencyId = currencyId,
            CategoryId = 1,
            IsTrading = true,
            CreatedBy = UserId,
            UpdatedBy = UserId,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });

        return instrumentId;
    }

    public void AddOperation(
        Guid portfolioId,
        OperationType type,
        string currencyId,
        DateTime tradeDate,
        Guid? instrumentId = null,
        decimal quantity = 0m,
        decimal price = 0m,
        decimal fee = 0m)
    {
        context.Operations.Add(new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            InstrumentId = instrumentId,
            Type = type,
            Quantity = quantity,
            Price = price,
            Fee = fee,
            CurrencyId = currencyId,
            TradeDate = DateTime.SpecifyKind(tradeDate, DateTimeKind.Utc),
            SettlementDate = DateTime.SpecifyKind(tradeDate, DateTimeKind.Utc),
            CreatedAt = DateTime.SpecifyKind(tradeDate, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(tradeDate, DateTimeKind.Utc)
        });
    }

    public void AddPrice(Guid instrumentId, string currencyId, DateTime date, decimal value, string provider = "MOEX")
    {
        context.Prices.Add(new Price
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Value = value,
            CurrencyId = currencyId,
            SourceCurrencyId = currencyId,
            Provider = provider,
            CreatedAt = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(date, DateTimeKind.Utc)
        });
    }

    public Task SaveChangesAsync() => context.SaveChangesAsync();

    public Task<Result<PortfoliosSummaryDto>> HandleAsync(GetPortfoliosSummaryQuery query) =>
        portfoliosSummaryHandler.Handle(query, CancellationToken.None);

    public async Task<PortfoliosSummaryDto> GetPortfoliosSummaryAsync(string? method = null, string? currency = null)
    {
        var result = await portfoliosSummaryHandler.Handle(
            new GetPortfoliosSummaryQuery(method, currency),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    public async Task<PortfolioSummaryDto> GetPortfolioSummaryAsync(Guid portfolioId)
    {
        var result = await portfolioSummaryHandler.Handle(
            new GetPortfolioSummaryQuery(portfolioId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);
        return result.Value!;
    }

    public async Task<PortfolioSummaryDto> GetAggregatePortfolioSummaryAsync(string? method = null, string? currency = null)
    {
        var result = await aggregateSummaryHandler.Handle(
            new GetAggregatePortfolioSummaryQuery(method, currency),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    public Task<Result<PortfolioSummaryDto>> HandleAggregateSummaryAsync(GetAggregatePortfolioSummaryQuery query) =>
        aggregateSummaryHandler.Handle(query, CancellationToken.None);

    public Task<Result<IReadOnlyCollection<PortfolioPerformanceDto>>?> HandlePortfolioPerformanceAsync(GetPortfolioPerformanceQuery query) =>
        portfolioPerformanceHandler.Handle(query, CancellationToken.None);

    public async Task<IReadOnlyCollection<PortfolioPerformanceDto>> GetPortfolioPerformanceAsync(
        Guid portfolioId,
        string? method = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var result = await portfolioPerformanceHandler.Handle(
            new GetPortfolioPerformanceQuery(portfolioId, method, from, to),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);
        return result.Value!;
    }

    public Task<Result<IReadOnlyCollection<PortfolioPerformanceDto>>> HandleAggregatePerformanceAsync(
        GetAggregatePortfolioPerformanceQuery query) =>
        aggregatePerformanceHandler.Handle(query, CancellationToken.None);

    public async Task<IReadOnlyCollection<PortfolioPerformanceDto>> GetAggregatePerformanceAsync(
        string? method = null,
        string? currency = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var result = await aggregatePerformanceHandler.Handle(
            new GetAggregatePortfolioPerformanceQuery(method, currency, from, to),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    public ValueTask DisposeAsync()
    {
        return database.DisposeAsync();
    }

    private void SeedReferenceData()
    {
        if (!context.Users.AsNoTracking().Any(x => x.Id == UserId))
        {
            context.Users.Add(new AppUser
            {
                Id = UserId,
                UserName = "summary-test",
                NormalizedUserName = "SUMMARY-TEST",
                Email = "summary-test@example.com",
                NormalizedEmail = "SUMMARY-TEST@EXAMPLE.COM"
            });
        }

        if (!context.Users.AsNoTracking().Any(x => x.Id == OtherUserId))
        {
            context.Users.Add(new AppUser
            {
                Id = OtherUserId,
                UserName = "summary-other-test",
                NormalizedUserName = "SUMMARY-OTHER-TEST",
                Email = "summary-other-test@example.com",
                NormalizedEmail = "SUMMARY-OTHER-TEST@EXAMPLE.COM"
            });
        }

        if (!context.Currencies.AsNoTracking().Any(x => x.Id == "RUB"))
        {
            context.Currencies.Add(new Currency { Id = "RUB" });
        }

        if (!context.Currencies.AsNoTracking().Any(x => x.Id == "USD"))
        {
            context.Currencies.Add(new Currency { Id = "USD" });
        }

        if (!context.Currencies.AsNoTracking().Any(x => x.Id == "EUR"))
        {
            context.Currencies.Add(new Currency { Id = "EUR" });
        }

        if (!context.Categories.AsNoTracking().Any(x => x.Id == 1))
        {
            context.Categories.Add(new Category { Id = 1, Name = "Stocks" });
        }

        if (!context.Brokers.AsNoTracking().Any(x => x.Id == BrokerId))
        {
            context.Brokers.Add(new Broker
            {
                Id = BrokerId,
                Code = "tbank-tests",
                Name = "T-Bank Tests"
            });
        }

        context.SaveChanges();
    }

    private sealed class FixedUserAccessor(Guid userId) : IUserAccessor
    {
        public Guid GetUserId() => userId;
    }
}
