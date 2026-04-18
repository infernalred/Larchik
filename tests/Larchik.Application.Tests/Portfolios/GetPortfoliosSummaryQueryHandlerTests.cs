using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.GetPortfolioSummary;
using Larchik.Application.Portfolios.GetPortfoliosSummary;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class GetPortfoliosSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_AggregatesMultiplePortfolioSummaries()
    {
        await using var harness = new SummaryHarness();
        var portfolio1Id = harness.AddPortfolio("Primary", "RUB");
        var portfolio2Id = harness.AddPortfolio("Secondary", "RUB");
        var instrumentId = harness.AddInstrument("LKOH", "RUB");
        var now = DateTime.UtcNow;

        harness.AddOperation(portfolio1Id, OperationType.Deposit, "RUB", now.AddDays(-10), price: 1000m);
        harness.AddOperation(portfolio1Id, OperationType.Buy, "RUB", now.AddDays(-9), instrumentId, quantity: 10m, price: 50m);
        harness.AddOperation(portfolio2Id, OperationType.Deposit, "RUB", now.AddDays(-8), price: 500m);
        harness.AddPrice(instrumentId, "RUB", now.AddDays(-1), 60m);
        await harness.SaveChangesAsync();

        var aggregate = await harness.GetPortfoliosSummaryAsync();
        var summary1 = await harness.GetPortfolioSummaryAsync(portfolio1Id);
        var summary2 = await harness.GetPortfolioSummaryAsync(portfolio2Id);

        Assert.Equal(2, aggregate.PortfolioCount);
        Assert.Equal("RUB", aggregate.ReportingCurrencyId);
        Assert.Equal("adjustingAvg", aggregate.ValuationMethod);
        Assert.Equal(summary1.NetInflowBase + summary2.NetInflowBase, aggregate.NetInflowBase);
        Assert.Equal(summary1.GrossDepositsBase + summary2.GrossDepositsBase, aggregate.GrossDepositsBase);
        Assert.Equal(summary1.GrossWithdrawalsBase + summary2.GrossWithdrawalsBase, aggregate.GrossWithdrawalsBase);
        Assert.Equal(summary1.CashBase + summary2.CashBase, aggregate.CashBase);
        Assert.Equal(summary1.PositionsValueBase + summary2.PositionsValueBase, aggregate.PositionsValueBase);
        Assert.Equal(summary1.RealizedBase + summary2.RealizedBase, aggregate.RealizedBase);
        Assert.Equal(summary1.UnrealizedBase + summary2.UnrealizedBase, aggregate.UnrealizedBase);
        Assert.Equal(summary1.NavBase + summary2.NavBase, aggregate.NavBase);
        Assert.Equal(summary1.PnlBase + summary2.PnlBase, aggregate.PnlBase);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenReportingCurrenciesDifferAndCurrencyIsNotSpecified()
    {
        await using var harness = new SummaryHarness();
        harness.AddPortfolio("Ruble", "RUB");
        harness.AddPortfolio("Dollar", "USD");
        await harness.SaveChangesAsync();

        var result = await harness.HandleAsync(new GetPortfoliosSummaryQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Portfolios use different reporting currencies. Specify the 'currency' query parameter.",
            result.Error);
    }

    [Fact]
    public async Task Handle_IgnoresPricesAfterCurrentAsOfDate()
    {
        await using var harness = new SummaryHarness();
        var portfolioId = harness.AddPortfolio("Primary", "RUB");
        var instrumentId = harness.AddInstrument("SBER", "RUB");
        var now = DateTime.UtcNow;

        harness.AddOperation(portfolioId, OperationType.Deposit, "RUB", now.AddDays(-5), price: 1000m);
        harness.AddOperation(portfolioId, OperationType.Buy, "RUB", now.AddDays(-4), instrumentId, quantity: 10m, price: 50m);
        harness.AddPrice(instrumentId, "RUB", now.AddMinutes(-1), 60m);
        harness.AddPrice(instrumentId, "RUB", now.AddDays(1), 999m);
        await harness.SaveChangesAsync();

        var result = await harness.GetPortfoliosSummaryAsync();

        Assert.Equal(500m, result.CashBase);
        Assert.Equal(600m, result.PositionsValueBase);
        Assert.Equal(1100m, result.NavBase);
        Assert.Equal(100m, result.UnrealizedBase);
        Assert.Equal(100m, result.PnlBase);
    }

    private sealed class SummaryHarness : IAsyncDisposable
    {
        private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid BrokerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly DateTime SeedTimestamp = new(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc);

        private readonly SqliteConnection _connection;
        private readonly LarchikContext _context;
        private readonly GetPortfoliosSummaryQueryHandler _portfoliosSummaryHandler;
        private readonly GetPortfolioSummaryQueryHandler _portfolioSummaryHandler;

        public SummaryHarness()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<LarchikContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new LarchikContext(options);
            _context.Database.EnsureCreated();

            SeedReferenceData();

            var userAccessor = new FixedUserAccessor(UserId);
            _portfoliosSummaryHandler = new GetPortfoliosSummaryQueryHandler(_context, userAccessor);
            _portfolioSummaryHandler = new GetPortfolioSummaryQueryHandler(_context, userAccessor);
        }

        public Guid AddPortfolio(string name, string reportingCurrencyId)
        {
            var portfolioId = Guid.NewGuid();
            _context.Portfolios.Add(new Portfolio
            {
                Id = portfolioId,
                UserId = UserId,
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
            _context.Instruments.Add(new Instrument
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
            _context.Operations.Add(new Operation
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
            _context.Prices.Add(new Price
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

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public Task<Result<PortfoliosSummaryDto>> HandleAsync(GetPortfoliosSummaryQuery query) =>
            _portfoliosSummaryHandler.Handle(query, CancellationToken.None);

        public async Task<PortfoliosSummaryDto> GetPortfoliosSummaryAsync(string? method = null, string? currency = null)
        {
            var result = await _portfoliosSummaryHandler.Handle(
                new GetPortfoliosSummaryQuery(method, currency),
                CancellationToken.None);

            Assert.True(result.IsSuccess, result.Error);
            return result.Value!;
        }

        public async Task<PortfolioSummaryDto> GetPortfolioSummaryAsync(Guid portfolioId)
        {
            var result = await _portfolioSummaryHandler.Handle(
                new GetPortfolioSummaryQuery(portfolioId),
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.IsSuccess, result.Error);
            return result.Value!;
        }

        public ValueTask DisposeAsync()
        {
            _context.Dispose();
            _connection.Dispose();
            return ValueTask.CompletedTask;
        }

        private void SeedReferenceData()
        {
            if (!_context.Users.AsNoTracking().Any(x => x.Id == UserId))
            {
                _context.Users.Add(new AppUser
                {
                    Id = UserId,
                    UserName = "summary-test",
                    NormalizedUserName = "SUMMARY-TEST",
                    Email = "summary-test@example.com",
                    NormalizedEmail = "SUMMARY-TEST@EXAMPLE.COM"
                });
            }

            if (!_context.Currencies.AsNoTracking().Any(x => x.Id == "RUB"))
            {
                _context.Currencies.Add(new Currency { Id = "RUB" });
            }

            if (!_context.Currencies.AsNoTracking().Any(x => x.Id == "USD"))
            {
                _context.Currencies.Add(new Currency { Id = "USD" });
            }

            if (!_context.Categories.AsNoTracking().Any(x => x.Id == 1))
            {
                _context.Categories.Add(new Category { Id = 1, Name = "Stocks" });
            }

            if (!_context.Brokers.AsNoTracking().Any(x => x.Id == BrokerId))
            {
                _context.Brokers.Add(new Broker
                {
                    Id = BrokerId,
                    Code = "tbank-tests",
                    Name = "T-Bank Tests"
                });
            }

            _context.SaveChanges();
        }

        private sealed class FixedUserAccessor(Guid userId) : IUserAccessor
        {
            public Guid GetUserId() => userId;
        }
    }
}
