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
    private readonly Dictionary<Guid, DateTime> _lastLoggedNextRunByDefinition = new();
    private readonly HashSet<Guid> _loggedDisabledDefinitions = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job scheduler started");

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
        await LogDefinitionScheduleState(context, cancellationToken);

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

                logger.LogInformation(
                    "Queued run for job {JobName} ({JobType}): dedup {DedupKey}, available at {AvailableAtUtc:O} UTC",
                    definition.Name,
                    definition.JobType,
                    spec.DedupKey,
                    spec.AvailableAtUtc);
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

        await LogDefinitionScheduleState(context, cancellationToken);
    }

    private async Task EnsureDefinitions(
        LarchikContext context,
        BackgroundJobsOptions options,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var fx = options.FxCbrDaily;
        await EnsureDefinition(
            context,
            now,
            cancellationToken,
            BackgroundJobConstants.FxCbrDailyDefinitionName,
            BackgroundJobConstants.FxCbrDailyJobType,
            fx.Enabled,
            fx.HourUtc,
            fx.MinuteUtc,
            fx.MaxAttempts,
            fx.RetryDelayMinutes,
            fx.LockTimeoutMinutes);

        var moex = options.MoexPricesDaily;
        await EnsureDefinition(
            context,
            now,
            cancellationToken,
            BackgroundJobConstants.MoexPricesDailyDefinitionName,
            BackgroundJobConstants.MoexPricesDailyJobType,
            moex.Enabled,
            moex.HourUtc,
            moex.MinuteUtc,
            moex.MaxAttempts,
            moex.RetryDelayMinutes,
            moex.LockTimeoutMinutes);

        var tbank = options.TbankPricesDaily;
        await EnsureDefinition(
            context,
            now,
            cancellationToken,
            BackgroundJobConstants.TbankPricesDailyDefinitionName,
            BackgroundJobConstants.TbankPricesDailyJobType,
            tbank.Enabled,
            tbank.HourUtc,
            tbank.MinuteUtc,
            tbank.MaxAttempts,
            tbank.RetryDelayMinutes,
            tbank.LockTimeoutMinutes);
    }

    private async Task EnsureDefinition(
        LarchikContext context,
        DateTime now,
        CancellationToken cancellationToken,
        string definitionName,
        string jobType,
        bool isEnabled,
        int hourUtc,
        int minuteUtc,
        int maxAttemptsRaw,
        int retryDelayMinutesRaw,
        int lockTimeoutMinutesRaw)
    {
        var scheduleValue = $"{hourUtc:00}:{minuteUtc:00}";
        var maxAttempts = Math.Max(1, maxAttemptsRaw);
        var retryDelayMinutes = Math.Max(1, retryDelayMinutesRaw);
        var lockTimeoutMinutes = Math.Max(1, lockTimeoutMinutesRaw);

        var definition = await context.JobDefinitions
            .FirstOrDefaultAsync(x => x.Name == definitionName, cancellationToken);

        if (definition is null)
        {
            definition = new JobDefinition
            {
                Id = Guid.NewGuid(),
                Name = definitionName,
                JobType = jobType,
                IsEnabled = isEnabled,
                ScheduleType = JobScheduleType.DailyUtc,
                ScheduleValue = scheduleValue,
                MaxAttempts = maxAttempts,
                RetryDelayMinutes = retryDelayMinutes,
                LockTimeoutMinutes = lockTimeoutMinutes,
                CreatedAt = now,
                UpdatedAt = now
            };

            definition.NextRunAt = JobScheduleCalculator.ComputeNextRunUtc(definition, now);
            context.JobDefinitions.Add(definition);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Created job definition {JobName} ({JobType}); enabled: {IsEnabled}; next run at {NextRunAtUtc:O} UTC",
                definition.Name,
                definition.JobType,
                definition.IsEnabled,
                definition.NextRunAt);
            return;
        }

        var scheduleChanged = definition.ScheduleType != JobScheduleType.DailyUtc ||
                              !string.Equals(definition.ScheduleValue, scheduleValue, StringComparison.Ordinal);

        var changed =
            !string.Equals(definition.JobType, jobType, StringComparison.Ordinal) ||
            definition.IsEnabled != isEnabled ||
            definition.ScheduleType != JobScheduleType.DailyUtc ||
            !string.Equals(definition.ScheduleValue, scheduleValue, StringComparison.Ordinal) ||
            definition.MaxAttempts != maxAttempts ||
            definition.RetryDelayMinutes != retryDelayMinutes ||
            definition.LockTimeoutMinutes != lockTimeoutMinutes;

        if (!changed && !scheduleChanged && definition.NextRunAt > now.AddDays(-1))
        {
            return;
        }

        definition.JobType = jobType;
        definition.IsEnabled = isEnabled;
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

        logger.LogInformation(
            "Updated job definition {JobName} ({JobType}); enabled: {IsEnabled}; next run at {NextRunAtUtc:O} UTC",
            definition.Name,
            definition.JobType,
            definition.IsEnabled,
            definition.NextRunAt);
    }

    private async Task LogDefinitionScheduleState(LarchikContext context, CancellationToken cancellationToken)
    {
        var definitions = await context.JobDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var existingIds = definitions.Select(x => x.Id).ToHashSet();

        foreach (var definition in definitions)
        {
            if (definition.IsEnabled)
            {
                _loggedDisabledDefinitions.Remove(definition.Id);
                var shouldLog = !_lastLoggedNextRunByDefinition.TryGetValue(definition.Id, out var lastNextRun) ||
                                lastNextRun != definition.NextRunAt;

                if (!shouldLog) continue;

                logger.LogInformation(
                    "Job {JobName} ({JobType}) is waiting for run at {NextRunAtUtc:O} UTC",
                    definition.Name,
                    definition.JobType,
                    definition.NextRunAt);

                _lastLoggedNextRunByDefinition[definition.Id] = definition.NextRunAt;
                continue;
            }

            _lastLoggedNextRunByDefinition.Remove(definition.Id);
            if (!_loggedDisabledDefinitions.Add(definition.Id)) continue;

            logger.LogInformation(
                "Job {JobName} ({JobType}) is disabled",
                definition.Name,
                definition.JobType);
        }

        foreach (var staleId in _lastLoggedNextRunByDefinition.Keys.Where(x => !existingIds.Contains(x)).ToArray())
        {
            _lastLoggedNextRunByDefinition.Remove(staleId);
        }

        foreach (var staleId in _loggedDisabledDefinitions.Where(x => !existingIds.Contains(x)).ToArray())
        {
            _loggedDisabledDefinitions.Remove(staleId);
        }
    }
}
