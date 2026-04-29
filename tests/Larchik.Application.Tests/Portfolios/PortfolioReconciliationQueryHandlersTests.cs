using Larchik.Application.Contracts;
using Larchik.Application.Common.Paging;
using Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlerts;
using Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlertsSummary;
using Larchik.Application.Portfolios.Reconciliation.GetLatestPortfolioReconciliationResult;
using Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationHistory;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class PortfolioReconciliationQueryHandlersTests
{
    [Fact]
    public async Task HistoryQuery_ReturnsOnlyCurrentUserEntries_AndSupportsFilters()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetPortfolioReconciliationHistoryQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetPortfolioReconciliationHistoryQuery(
                Status: "mismatch",
                Severity: "critical",
                AlertRequired: true,
                Paging: new PageQuery { Page = 1, PageSize = 10 }),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(result.Value!.Items);
        Assert.Equal(harness.OwnCriticalResultId, result.Value!.Items.Single().Id);
    }

    [Fact]
    public async Task HistoryQuery_AppliesCaseInsensitiveSeverityAndStatusFilters()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetPortfolioReconciliationHistoryQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetPortfolioReconciliationHistoryQuery(
                Status: "Mismatch",
                Severity: "Critical"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(result.Value!.Items);
        Assert.Equal(harness.OwnCriticalResultId, result.Value.Items.Single().Id);
    }

    [Fact]
    public async Task LatestQuery_ReturnsLatestEntryForPortfolio()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetLatestPortfolioReconciliationResultQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetLatestPortfolioReconciliationResultQuery(harness.OwnPortfolioId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(harness.OwnCriticalResultId, result.Value!.Id);
        Assert.Equal("critical", result.Value.Severity);
    }

    [Fact]
    public async Task HistoryQuery_ReturnsFailureForInvalidSortField()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetPortfolioReconciliationHistoryQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetPortfolioReconciliationHistoryQuery(SortBy: "garbage"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("REC_INVALID_SORT_BY:", result.Error);
    }

    [Fact]
    public async Task AlertsQuery_ReturnsOnlyAlertRequiredEntriesForCurrentUser()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetPortfolioReconciliationAlertsQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetPortfolioReconciliationAlertsQuery(
                Paging: new PageQuery { Page = 1, PageSize = 10 }),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(result.Value!.Items);
        Assert.Equal(harness.OwnCriticalResultId, result.Value.Items.Single().Id);
    }

    [Fact]
    public async Task AlertsQuery_AppliesCaseInsensitiveSeverityFilter()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetPortfolioReconciliationAlertsQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetPortfolioReconciliationAlertsQuery(Severity: "Critical"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(result.Value!.Items);
        Assert.Equal(harness.OwnCriticalResultId, result.Value.Items.Single().Id);
    }

    [Fact]
    public async Task AlertsQuery_ReturnsFailureForInvalidSeverity()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetPortfolioReconciliationAlertsQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetPortfolioReconciliationAlertsQuery(Severity: "bad"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("REC_INVALID_SEVERITY:", result.Error);
    }

    [Fact]
    public async Task AlertsSummaryQuery_ReturnsAggregatesAndLatestCriticalByPortfolio()
    {
        await using var harness = await ReconciliationQueryHarness.CreateAsync();
        var handler = new GetPortfolioReconciliationAlertsSummaryQueryHandler(harness.Context, new FixedUserAccessor(harness.UserId));

        var result = await handler.Handle(
            new GetPortfolioReconciliationAlertsSummaryQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, result.Value!.TotalAlerts);
        Assert.Equal(1, result.Value.CriticalAlerts);
        Assert.Equal(0, result.Value.WarningAlerts);
        Assert.Single(result.Value.LatestCriticalByPortfolio);
        Assert.Equal(harness.OwnCriticalResultId, result.Value.LatestCriticalByPortfolio.Single().Id);
    }

    private sealed class FixedUserAccessor(Guid userId) : IUserAccessor
    {
        public Guid GetUserId() => userId;
    }

    private sealed class ReconciliationQueryHarness(SqliteTestDatabase database) : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public Guid OtherUserId { get; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public Guid OwnPortfolioId { get; } = Guid.NewGuid();
        public Guid OwnCriticalResultId { get; } = Guid.NewGuid();
        public LarchikContext Context => database.Context;

        public static async Task<ReconciliationQueryHarness> CreateAsync()
        {
            var harness = new ReconciliationQueryHarness(SqliteTestContextFactory.Create());
            await harness.SeedAsync();
            return harness;
        }

        private async Task SeedAsync()
        {
            var brokerId = Guid.NewGuid();
            var otherPortfolioId = Guid.NewGuid();
            var now = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc);

            Context.Users.AddRange(
                new AppUser { Id = UserId, UserName = "u1", NormalizedUserName = "U1" },
                new AppUser { Id = OtherUserId, UserName = "u2", NormalizedUserName = "U2" });
            var existingBroker = await Context.Brokers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "tbank");
            if (existingBroker is null)
            {
                Context.Brokers.Add(new Broker { Id = brokerId, Code = "tbank", Name = "T-Bank" });
            }
            else
            {
                brokerId = existingBroker.Id;
            }
            if (!await Context.Currencies.AnyAsync(x => x.Id == "RUB"))
            {
                Context.Currencies.Add(new Currency { Id = "RUB" });
            }
            Context.Portfolios.AddRange(
                new Portfolio
                {
                    Id = OwnPortfolioId,
                    UserId = UserId,
                    BrokerId = brokerId,
                    Name = "Own",
                    ReportingCurrencyId = "RUB",
                    CreatedAt = now
                },
                new Portfolio
                {
                    Id = otherPortfolioId,
                    UserId = OtherUserId,
                    BrokerId = brokerId,
                    Name = "Other",
                    ReportingCurrencyId = "RUB",
                    CreatedAt = now
                });
            Context.PortfolioReconciliationResults.AddRange(
                new PortfolioReconciliationResult
                {
                    Id = Guid.NewGuid(),
                    PortfolioId = OwnPortfolioId,
                    StatementDate = now.AddDays(-1),
                    Source = "reconciliation.daily",
                    ReportingCurrencyId = "RUB",
                    Status = "matched",
                    Severity = "info",
                    AlertRequired = false,
                    ReasonCode = "within_tolerance",
                    ToleranceBase = 1m,
                    CreatedAt = now.AddDays(-1)
                },
                new PortfolioReconciliationResult
                {
                    Id = OwnCriticalResultId,
                    PortfolioId = OwnPortfolioId,
                    StatementDate = now,
                    Source = "reconciliation.daily",
                    ReportingCurrencyId = "RUB",
                    Status = "mismatch",
                    Severity = "critical",
                    AlertRequired = true,
                    ReasonCode = "delta_exceeds_tolerance",
                    ToleranceBase = 1m,
                    NavDelta = 100m,
                    CashDelta = 10m,
                    PositionsDelta = 90m,
                    CreatedAt = now
                },
                new PortfolioReconciliationResult
                {
                    Id = Guid.NewGuid(),
                    PortfolioId = otherPortfolioId,
                    StatementDate = now,
                    Source = "reconciliation.daily",
                    ReportingCurrencyId = "RUB",
                    Status = "mismatch",
                    Severity = "critical",
                    AlertRequired = true,
                    ReasonCode = "delta_exceeds_tolerance",
                    ToleranceBase = 1m,
                    CreatedAt = now
                });

            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }
    }
}
