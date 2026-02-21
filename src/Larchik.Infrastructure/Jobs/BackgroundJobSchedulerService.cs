using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.Jobs;

public class BackgroundJobSchedulerService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    IEnumerable<IJobRunPlanner> planners,
    ILogger<BackgroundJobSchedulerService> logger)
    : BackgroundService
{
    private readonly IReadOnlyDictionary<string, IJobRunPlanner> _planners = planners
        .ToDictionary(x => x.JobType, x => x, StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = optionsMonitor.CurrentValue;

            if (options.Enabled)
            {
                try
                {
                    await ScheduleDueJobs(options, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background job scheduler tick failed");
                }
            }

            var delaySeconds = Math.Max(5, options.SchedulerPollSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task ScheduleDueJobs(BackgroundJobsOptions options, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LarchikContext>();

        await EnsureDefinitions(context, options, now, cancellationToken);

        var dueDefinitions = await context.JobDefinitions
            .Where(x => x.IsEnabled && x.NextRunAt <= now)
            .OrderBy(x => x.NextRunAt)
            .ToListAsync(cancellationToken);

        if (dueDefinitions.Count == 0) return;

        foreach (var definition in dueDefinitions)
        {
            if (!_planners.TryGetValue(definition.JobType, out var planner))
            {
                logger.LogWarning("No planner registered for job type {JobType}", definition.JobType);
                definition.NextRunAt = JobScheduleCalculator.ComputeNextRunUtc(definition, now);
                definition.UpdatedAt = now;
                continue;
            }

            var specs = planner.BuildRuns(definition, now);
            foreach (var spec in specs)
            {
                var exists = await context.JobRuns
                    .AsNoTracking()
                    .AnyAsync(x => x.DedupKey == spec.DedupKey, cancellationToken);

                if (exists) continue;

                context.JobRuns.Add(new JobRun
                {
                    Id = Guid.NewGuid(),
                    JobDefinitionId = definition.Id,
                    DedupKey = spec.DedupKey,
                    PayloadJson = spec.PayloadJson,
                    Status = JobRunStatus.Pending,
                    Attempt = 0,
                    MaxAttempts = definition.MaxAttempts,
                    AvailableAt = spec.AvailableAtUtc,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            definition.NextRunAt = JobScheduleCalculator.ComputeNextRunUtc(definition, now);
            definition.UpdatedAt = now;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Most likely duplicate dedup_key in race between instances.
            logger.LogWarning(ex, "Job scheduling had conflicts, next tick will reconcile");
        }
    }

    private async Task EnsureDefinitions(
        LarchikContext context,
        BackgroundJobsOptions options,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var fx = options.FxCbrDaily;
        var scheduleValue = $"{fx.HourUtc:00}:{fx.MinuteUtc:00}";

        var definition = await context.JobDefinitions
            .FirstOrDefaultAsync(x => x.Name == BackgroundJobConstants.FxCbrDailyDefinitionName, cancellationToken);

        if (definition is null)
        {
            definition = new JobDefinition
            {
                Id = Guid.NewGuid(),
                Name = BackgroundJobConstants.FxCbrDailyDefinitionName,
                JobType = BackgroundJobConstants.FxCbrDailyJobType,
                IsEnabled = fx.Enabled,
                ScheduleType = JobScheduleType.DailyUtc,
                ScheduleValue = scheduleValue,
                MaxAttempts = Math.Max(1, fx.MaxAttempts),
                RetryDelayMinutes = Math.Max(1, fx.RetryDelayMinutes),
                LockTimeoutMinutes = Math.Max(1, fx.LockTimeoutMinutes),
                CreatedAt = now,
                UpdatedAt = now
            };
            definition.NextRunAt = JobScheduleCalculator.ComputeNextRunUtc(definition, now);
            context.JobDefinitions.Add(definition);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var maxAttempts = Math.Max(1, fx.MaxAttempts);
        var retryDelayMinutes = Math.Max(1, fx.RetryDelayMinutes);
        var lockTimeoutMinutes = Math.Max(1, fx.LockTimeoutMinutes);

        var scheduleChanged = definition.ScheduleType != JobScheduleType.DailyUtc ||
                              !string.Equals(definition.ScheduleValue, scheduleValue, StringComparison.Ordinal);

        var changed =
            !string.Equals(definition.JobType, BackgroundJobConstants.FxCbrDailyJobType, StringComparison.Ordinal) ||
            definition.IsEnabled != fx.Enabled ||
            definition.ScheduleType != JobScheduleType.DailyUtc ||
            !string.Equals(definition.ScheduleValue, scheduleValue, StringComparison.Ordinal) ||
            definition.MaxAttempts != maxAttempts ||
            definition.RetryDelayMinutes != retryDelayMinutes ||
            definition.LockTimeoutMinutes != lockTimeoutMinutes;

        if (!changed && !scheduleChanged && definition.NextRunAt > now.AddDays(-1))
        {
            return;
        }

        definition.JobType = BackgroundJobConstants.FxCbrDailyJobType;
        definition.IsEnabled = fx.Enabled;
        definition.ScheduleType = JobScheduleType.DailyUtc;
        definition.ScheduleValue = scheduleValue;
        definition.MaxAttempts = maxAttempts;
        definition.RetryDelayMinutes = retryDelayMinutes;
        definition.LockTimeoutMinutes = lockTimeoutMinutes;
        definition.UpdatedAt = now;

        if (scheduleChanged || definition.NextRunAt <= now.AddDays(-1))
        {
            definition.NextRunAt = JobScheduleCalculator.ComputeNextRunUtc(definition, now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
