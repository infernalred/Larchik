using System.Security.Claims;
using System.Reflection;
using Larchik.Infrastructure.Jobs;
using Larchik.Infrastructure.Security;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Larchik.Application.Tests.Infrastructure;

public sealed class InfrastructureBehaviorTests
{
    [Fact]
    public async Task BackgroundJobExecutor_DoesNotFinalizeRunAfterLockOwnershipChanges()
    {
        await using var database = SqliteTestContextFactory.Create();
        var definitionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);

        database.Context.JobDefinitions.Add(new JobDefinition
        {
            Id = definitionId,
            Name = "test",
            JobType = "test.job",
            IsEnabled = true,
            ScheduleType = JobScheduleType.IntervalMinutes,
            ScheduleValue = "60",
            NextRunAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        database.Context.JobRuns.Add(new JobRun
        {
            Id = runId,
            JobDefinitionId = definitionId,
            DedupKey = "test:1",
            PayloadJson = "",
            Status = JobRunStatus.Running,
            Attempt = 0,
            MaxAttempts = 3,
            AvailableAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        await database.Context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<LarchikContext>(options => options.UseSqlite(database.Connection));
        services.AddSingleton<IBackgroundJobHandler>(new LockStealingJobHandler(database.Connection, runId));
        services.AddSingleton<IOptionsMonitor<BackgroundJobsOptions>>(
            new StaticOptionsMonitor<BackgroundJobsOptions>(new BackgroundJobsOptions()));

        await using var provider = services.BuildServiceProvider();
        var executor = ActivatorUtilities.CreateInstance<BackgroundJobExecutorService>(provider);
        var workerId = (string)typeof(BackgroundJobExecutorService)
            .GetField("_workerId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(executor)!;

        database.Context.ChangeTracker.Clear();
        await database.Context.JobRuns
            .Where(x => x.Id == runId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LockedBy, workerId)
                .SetProperty(x => x.LockedUntilAt, now.AddMinutes(10)));

        var method = typeof(BackgroundJobExecutorService)
            .GetMethod("ExecuteClaimedRun", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(executor, [runId, CancellationToken.None])!;
        await task;

        database.Context.ChangeTracker.Clear();
        var run = await database.Context.JobRuns.SingleAsync(x => x.Id == runId);
        Assert.Equal(JobRunStatus.Running, run.Status);
        Assert.Equal("other-worker", run.LockedBy);
        Assert.Null(run.CompletedAt);
    }

    [Fact]
    public void JobScheduleCalculator_UsesConfiguredDailyTime_OrNextDayIfAlreadyPassed()
    {
        var definition = new JobDefinition
        {
            ScheduleType = JobScheduleType.DailyUtc,
            ScheduleValue = "05:10"
        };

        var sameDay = JobScheduleCalculator.ComputeNextRunUtc(
            definition,
            new DateTime(2026, 4, 20, 5, 0, 0, DateTimeKind.Utc));
        var nextDay = JobScheduleCalculator.ComputeNextRunUtc(
            definition,
            new DateTime(2026, 4, 20, 5, 10, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 4, 20, 5, 10, 0, DateTimeKind.Utc), sameDay);
        Assert.Equal(new DateTime(2026, 4, 21, 5, 10, 0, DateTimeKind.Utc), nextDay);
    }

    [Fact]
    public void JobScheduleCalculator_FallsBack_ForInvalidScheduleValues()
    {
        var daily = new JobDefinition
        {
            ScheduleType = JobScheduleType.DailyUtc,
            ScheduleValue = "broken"
        };
        var interval = new JobDefinition
        {
            ScheduleType = JobScheduleType.IntervalMinutes,
            ScheduleValue = "0"
        };
        var unknown = new JobDefinition
        {
            ScheduleType = (JobScheduleType)999,
            ScheduleValue = "ignored"
        };
        var now = new DateTime(2026, 4, 20, 4, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 4, 20, 5, 10, 0, DateTimeKind.Utc), JobScheduleCalculator.ComputeNextRunUtc(daily, now));
        Assert.Equal(now.AddMinutes(60), JobScheduleCalculator.ComputeNextRunUtc(interval, now));
        Assert.Equal(now.AddMinutes(5), JobScheduleCalculator.ComputeNextRunUtc(unknown, now));
    }

    [Fact]
    public void RunPlanners_ProduceExpectedDedupKeys()
    {
        var definition = new JobDefinition();
        var now = new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc);

        var fxRuns = new FxCbrDailyRunPlanner().BuildRuns(definition, now).ToArray();
        var moexRuns = new MoexPricesDailyRunPlanner().BuildRuns(definition, now).ToArray();
        var tbankRuns = new TbankPricesDailyRunPlanner().BuildRuns(definition, now).ToArray();
        var instrumentInfoRuns = new TbankInstrumentInfoDailyRunPlanner().BuildRuns(definition, now).ToArray();

        Assert.Equal(["fx:cbr:2026-04-20", "fx:cbr:2026-04-19"], fxRuns.Select(x => x.DedupKey));
        Assert.Equal(["prices:moex:2026-04-20", "prices:moex:2026-04-19"], moexRuns.Select(x => x.DedupKey));
        Assert.Equal(["prices:tbank:2026-04-20", "prices:tbank:2026-04-19"], tbankRuns.Select(x => x.DedupKey));
        Assert.Single(instrumentInfoRuns);
        Assert.Equal("instrument-info:tbank:2026-04-20", instrumentInfoRuns[0].DedupKey);
    }

    [Fact]
    public void UserAccessor_ReadsNameIdentifierClaim()
    {
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ]))
        };
        var accessor = new UserAccessor(new HttpContextAccessor { HttpContext = httpContext });

        var actual = accessor.GetUserId();

        Assert.Equal(userId, actual);
    }

    [Fact]
    public void UserAccessor_ThrowsClearError_WhenClaimIsMissing()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        var accessor = new UserAccessor(new HttpContextAccessor { HttpContext = httpContext });

        var error = Assert.Throws<InvalidOperationException>(() => accessor.GetUserId());

        Assert.Equal("Authenticated user id claim is missing.", error.Message);
    }

    private sealed class LockStealingJobHandler(SqliteConnection connection, Guid runId) : IBackgroundJobHandler
    {
        public string JobType => "test.job";

        public async Task<JobExecutionResult> ExecuteAsync(string payloadJson, CancellationToken cancellationToken)
        {
            var options = new DbContextOptionsBuilder<LarchikContext>()
                .UseSqlite(connection)
                .Options;
            await using var context = new LarchikContext(options);

            await context.JobRuns
                .Where(x => x.Id == runId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LockedBy, "other-worker")
                    .SetProperty(x => x.LockedUntilAt, DateTime.UtcNow.AddMinutes(10)), cancellationToken);

            return JobExecutionResult.Success();
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
