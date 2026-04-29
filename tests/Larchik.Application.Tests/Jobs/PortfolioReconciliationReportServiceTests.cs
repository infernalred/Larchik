using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Infrastructure.Jobs;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Larchik.Application.Tests.Jobs;

public sealed class PortfolioReconciliationReportServiceTests
{
    [Fact]
    public async Task LogDailyReportAsync_LogsWarning_WhenDeltaExceedsTolerance()
    {
        await using var database = SqliteTestContextFactory.Create();
        var (portfolioId, runDate) = await SeedPortfolioData(database.Context);
        var entries = new List<LogEntry>();
        var service = CreateService(database.Context, entries, new BackgroundJobsOptions
        {
            PortfolioReconciliationDaily = new PortfolioReconciliationDailyJobOptions
            {
                Enabled = true,
                DeltaToleranceBase = 1m,
                Targets =
                [
                    new PortfolioReconciliationTargetOptions
                    {
                        PortfolioId = portfolioId,
                        Date = runDate.ToString("yyyy-MM-dd"),
                        NavBase = 980m,
                        CashBase = 390m,
                        PositionsValueBase = 590m
                    }
                ]
            }
        });

        await service.LogDailyReportAsync(runDate, "prices.tbank.daily", CancellationToken.None);

        Assert.Contains(entries, x => x.Level == LogLevel.Warning && x.Message.Contains("reconciliation mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LogDailyReportAsync_LogsInformation_WhenWithinTolerance()
    {
        await using var database = SqliteTestContextFactory.Create();
        var (portfolioId, runDate) = await SeedPortfolioData(database.Context);
        var entries = new List<LogEntry>();
        var service = CreateService(database.Context, entries, new BackgroundJobsOptions
        {
            PortfolioReconciliationDaily = new PortfolioReconciliationDailyJobOptions
            {
                Enabled = true,
                DeltaToleranceBase = 1m,
                Targets =
                [
                    new PortfolioReconciliationTargetOptions
                    {
                        PortfolioId = portfolioId,
                        Date = runDate.ToString("yyyy-MM-dd"),
                        NavBase = 1000.5m,
                        CashBase = 399.5m,
                        PositionsValueBase = 600m
                    }
                ]
            }
        });

        await service.LogDailyReportAsync(runDate, "prices.moex.daily", CancellationToken.None);

        Assert.DoesNotContain(entries, x => x.Level == LogLevel.Warning);
        Assert.Contains(entries, x => x.Level == LogLevel.Information && x.Message.Contains("within tolerance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LogDailyReportAsync_ConvertsTargetCurrency_WhenConfigured()
    {
        await using var database = SqliteTestContextFactory.Create();
        var (portfolioId, runDate) = await SeedPortfolioData(database.Context, includeMarketUsdRubPrice: true);
        var entries = new List<LogEntry>();
        var service = CreateService(database.Context, entries, new BackgroundJobsOptions
        {
            PortfolioReconciliationDaily = new PortfolioReconciliationDailyJobOptions
            {
                Enabled = true,
                DeltaToleranceBase = 1m,
                Targets =
                [
                    new PortfolioReconciliationTargetOptions
                    {
                        PortfolioId = portfolioId,
                        Date = runDate.ToString("yyyy-MM-dd"),
                        CurrencyId = "USD",
                        NavBase = 10m,
                        CashBase = 4m,
                        PositionsValueBase = 6m
                    }
                ]
            }
        });

        await service.LogDailyReportAsync(runDate, "prices.moex.daily", CancellationToken.None);

        Assert.DoesNotContain(entries, x => x.Level == LogLevel.Warning);
        Assert.Contains(entries, x => x.Message.Contains("currency conversion applied", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, x => x.Message.Contains("within tolerance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LogDailyReportAsync_UsesBrokerCashLedgerSemantics_ForImportedTbankTrades()
    {
        await using var database = SqliteTestContextFactory.Create();
        var (portfolioId, runDate) = await SeedPortfolioData(database.Context, includeImportedSettlementGapTrade: true);
        var entries = new List<LogEntry>();
        var service = CreateService(database.Context, entries, new BackgroundJobsOptions
        {
            PortfolioReconciliationDaily = new PortfolioReconciliationDailyJobOptions
            {
                Enabled = true,
                DeltaToleranceBase = 0.01m,
                Targets =
                [
                    new PortfolioReconciliationTargetOptions
                    {
                        PortfolioId = portfolioId,
                        Date = runDate.ToString("yyyy-MM-dd"),
                        NavBase = 1200m,
                        CashBase = 400m,
                        PositionsValueBase = 800m
                    }
                ]
            }
        });

        await service.LogDailyReportAsync(runDate, "prices.tbank.daily", CancellationToken.None);

        Assert.DoesNotContain(entries, x => x.Level == LogLevel.Warning && x.Message.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, x => x.Level == LogLevel.Information && x.Message.Contains("within tolerance", StringComparison.OrdinalIgnoreCase));
    }

    private static PortfolioReconciliationReportService CreateService(
        LarchikContext context,
        List<LogEntry> entries,
        BackgroundJobsOptions options)
    {
        var logger = new ListLogger<PortfolioReconciliationReportService>(entries);
        return new PortfolioReconciliationReportService(
            context,
            new StaticOptionsMonitor<BackgroundJobsOptions>(options),
            logger);
    }

    private static async Task<(Guid PortfolioId, DateOnly RunDate)> SeedPortfolioData(
        LarchikContext context,
        bool includeMarketUsdRubPrice = false,
        bool includeImportedSettlementGapTrade = false)
    {
        var userId = Guid.NewGuid();
        var brokerId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var date = new DateOnly(2026, 4, 20);
        var tradeDateUtc = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var priceDateUtc = new DateTime(2026, 4, 20, 18, 0, 0, DateTimeKind.Utc);

        context.Users.Add(new AppUser
        {
            Id = userId,
            UserName = "recon-user",
            NormalizedUserName = "RECON-USER"
        });
        var existingTbankBroker = await context.Brokers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == "tbank");
        if (existingTbankBroker is null)
        {
            context.Brokers.Add(new Broker
            {
                Id = brokerId,
                Code = "tbank",
                Name = "T-Bank"
            });
        }
        else
        {
            brokerId = existingTbankBroker.Id;
        }
        if (!await context.Currencies.AnyAsync(x => x.Id == "RUB"))
        {
            context.Currencies.Add(new Currency { Id = "RUB" });
        }

        if (!await context.Categories.AnyAsync(x => x.Id == 1))
        {
            context.Categories.Add(new Category { Id = 1, Name = "Stocks" });
        }
        context.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            UserId = userId,
            BrokerId = brokerId,
            Name = "Main",
            ReportingCurrencyId = "RUB",
            CreatedAt = tradeDateUtc
        });
        context.Instruments.Add(new Instrument
        {
            Id = instrumentId,
            Name = "SBER",
            Ticker = "SBER",
            Isin = "RU0009029540",
            Type = InstrumentType.Equity,
            CurrencyId = "RUB",
            CategoryId = 1,
            CreatedBy = userId,
            UpdatedBy = userId,
            CreatedAt = tradeDateUtc,
            UpdatedAt = tradeDateUtc
        });
        context.Operations.Add(new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            Type = OperationType.Deposit,
            CurrencyId = "RUB",
            Price = 1000m,
            TradeDate = tradeDateUtc,
            SettlementDate = tradeDateUtc,
            CreatedAt = tradeDateUtc,
            UpdatedAt = tradeDateUtc
        });
        context.Operations.Add(new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            InstrumentId = instrumentId,
            Type = OperationType.Buy,
            CurrencyId = "RUB",
            Quantity = 3m,
            Price = 200m,
            Fee = 0m,
            TradeDate = tradeDateUtc,
            SettlementDate = tradeDateUtc,
            CreatedAt = tradeDateUtc,
            UpdatedAt = tradeDateUtc
        });

        if (includeImportedSettlementGapTrade)
        {
            context.Operations.Add(new Operation
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                InstrumentId = instrumentId,
                Type = OperationType.Buy,
                CurrencyId = "RUB",
                Quantity = 1m,
                Price = 100m,
                Fee = 0m,
                BrokerOperationKey = "v2:imported:000001",
                TradeDate = tradeDateUtc,
                SettlementDate = tradeDateUtc.AddDays(1),
                CreatedAt = tradeDateUtc,
                UpdatedAt = tradeDateUtc
            });
        }
        context.Prices.Add(new Price
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            Date = priceDateUtc,
            Value = 200m,
            CurrencyId = "RUB",
            SourceCurrencyId = "RUB",
            Provider = "MOEX",
            CreatedAt = priceDateUtc,
            UpdatedAt = priceDateUtc
        });

        if (includeMarketUsdRubPrice)
        {
            if (!await context.Currencies.AnyAsync(x => x.Id == "USD"))
            {
                context.Currencies.Add(new Currency { Id = "USD" });
            }

            var currencyInstrumentId = Guid.NewGuid();
            context.Instruments.Add(new Instrument
            {
                Id = currencyInstrumentId,
                Name = "USDRUB TOM",
                Ticker = "USDRUB_TOM",
                Isin = "USDRUB_TOM",
                Type = InstrumentType.Currency,
                CurrencyId = "RUB",
                CategoryId = 1,
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = tradeDateUtc,
                UpdatedAt = tradeDateUtc
            });
            context.Prices.Add(new Price
            {
                Id = Guid.NewGuid(),
                InstrumentId = currencyInstrumentId,
                Date = priceDateUtc,
                Value = 100m,
                CurrencyId = "RUB",
                SourceCurrencyId = "RUB",
                Provider = "MOEX",
                CreatedAt = priceDateUtc,
                UpdatedAt = priceDateUtc
            });
        }

        await context.SaveChangesAsync();
        return (portfolioId, date);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger<T>(List<LogEntry> entries) : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
