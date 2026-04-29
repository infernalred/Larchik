using Larchik.Application.Contracts;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Infrastructure.Jobs;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Larchik.Application.Tests.Jobs;

public sealed class PortfolioReconciliationDailyJobHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_RecalculatesTargetsBeforeRunningReconciliationReport()
    {
        var calls = new List<string>();
        var portfolioId = Guid.NewGuid();
        var options = new BackgroundJobsOptions
        {
            PortfolioReconciliationDaily = new PortfolioReconciliationDailyJobOptions
            {
                Enabled = true,
                Targets =
                [
                    new PortfolioReconciliationTargetOptions
                    {
                        PortfolioId = portfolioId,
                        Date = "2026-04-20",
                        NavBase = 1000m,
                        CashBase = 400m,
                        PositionsValueBase = 600m
                    }
                ]
            }
        };
        var recalc = new SpyRecalcService(calls);
        var report = new SpyReconciliationReportService(calls);
        var handler = new PortfolioReconciliationDailyJobHandler(
            report,
            recalc,
            new StaticOptionsMonitor<BackgroundJobsOptions>(options),
            NullLogger<PortfolioReconciliationDailyJobHandler>.Instance);

        var result = await handler.ExecuteAsync("""{"date":"2026-04-20"}""", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                $"recalc:{portfolioId}:2026-04-20",
                "report:2026-04-20:reconciliation.daily"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_JobPipeline_RecalculatesThenPersistsReconciliationResult()
    {
        await using var database = SqliteTestContextFactory.Create();
        var calls = new List<string>();
        var portfolioId = Guid.NewGuid();
        var statementDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var options = new BackgroundJobsOptions
        {
            PortfolioReconciliationDaily = new PortfolioReconciliationDailyJobOptions
            {
                Enabled = true,
                Targets =
                [
                    new PortfolioReconciliationTargetOptions
                    {
                        PortfolioId = portfolioId,
                        Date = "2026-04-20",
                        NavBase = 1000m,
                        CashBase = 400m,
                        PositionsValueBase = 600m
                    }
                ]
            }
        };
        var recalc = new SpyRecalcService(calls);
        var report = new PersistingSpyReconciliationReportService(database.Context, calls, portfolioId);
        var userId = Guid.NewGuid();
        var broker = await database.Context.Brokers.FirstOrDefaultAsync(x => x.Code == "pipeline-broker");
        if (broker is null)
        {
            broker = new Broker { Id = Guid.NewGuid(), Code = "pipeline-broker", Name = "Pipeline Broker" };
            database.Context.Brokers.Add(broker);
        }
        if (!await database.Context.Currencies.AnyAsync(x => x.Id == "RUB"))
        {
            database.Context.Currencies.Add(new Currency { Id = "RUB" });
        }
        database.Context.Users.Add(new AppUser { Id = userId, UserName = "pipeline-user", NormalizedUserName = "PIPELINE-USER" });
        database.Context.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            UserId = userId,
            BrokerId = broker.Id,
            Name = "Pipeline",
            ReportingCurrencyId = "RUB",
            CreatedAt = statementDate
        });
        await database.Context.SaveChangesAsync();

        var handler = new PortfolioReconciliationDailyJobHandler(
            report,
            recalc,
            new StaticOptionsMonitor<BackgroundJobsOptions>(options),
            NullLogger<PortfolioReconciliationDailyJobHandler>.Instance);

        var result = await handler.ExecuteAsync("""{"date":"2026-04-20"}""", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                $"recalc:{portfolioId}:2026-04-20",
                "report:2026-04-20:reconciliation.daily"
            ],
            calls);
        Assert.True(await database.Context.PortfolioReconciliationResults
            .AnyAsync(x => x.PortfolioId == portfolioId
                           && x.StatementDate == statementDate
                           && x.Source == "reconciliation.daily"));
    }

    [Fact]
    public async Task ExecuteAsync_IncludesParseableTargetDateFormats_ForRecalcSelection()
    {
        var calls = new List<string>();
        var portfolioId = Guid.NewGuid();
        var options = new BackgroundJobsOptions
        {
            PortfolioReconciliationDaily = new PortfolioReconciliationDailyJobOptions
            {
                Enabled = true,
                Targets =
                [
                    new PortfolioReconciliationTargetOptions
                    {
                        PortfolioId = portfolioId,
                        Date = "2026-4-20",
                        NavBase = 1000m,
                        CashBase = 400m,
                        PositionsValueBase = 600m
                    }
                ]
            }
        };
        var recalc = new SpyRecalcService(calls);
        var report = new SpyReconciliationReportService(calls);
        var handler = new PortfolioReconciliationDailyJobHandler(
            report,
            recalc,
            new StaticOptionsMonitor<BackgroundJobsOptions>(options),
            NullLogger<PortfolioReconciliationDailyJobHandler>.Instance);

        var result = await handler.ExecuteAsync("""{"date":"2026-04-20"}""", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains($"recalc:{portfolioId}:2026-04-20", calls);
    }

    private sealed class SpyRecalcService(List<string> calls) : IPortfolioRecalcService
    {
        public Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default)
        {
            calls.Add($"recalc:{portfolioId}:{fromDate:yyyy-MM-dd}");
            return Task.CompletedTask;
        }
    }

    private sealed class SpyReconciliationReportService(List<string> calls) : IPortfolioReconciliationReportService
    {
        public Task LogDailyReportAsync(DateOnly runDate, string source, CancellationToken cancellationToken)
        {
            calls.Add($"report:{runDate:yyyy-MM-dd}:{source}");
            return Task.CompletedTask;
        }
    }

    private sealed class PersistingSpyReconciliationReportService(
        Larchik.Persistence.Context.LarchikContext context,
        List<string> calls,
        Guid portfolioId) : IPortfolioReconciliationReportService
    {
        public async Task LogDailyReportAsync(DateOnly runDate, string source, CancellationToken cancellationToken)
        {
            calls.Add($"report:{runDate:yyyy-MM-dd}:{source}");
            context.PortfolioReconciliationResults.Add(new PortfolioReconciliationResult
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                StatementDate = DateTime.SpecifyKind(runDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                Source = source,
                ReportingCurrencyId = "RUB",
                Status = "mismatch",
                Severity = "critical",
                AlertRequired = true,
                ReasonCode = "delta_exceeds_tolerance",
                ToleranceBase = 1m,
                CreatedAt = DateTime.SpecifyKind(runDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            });
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
