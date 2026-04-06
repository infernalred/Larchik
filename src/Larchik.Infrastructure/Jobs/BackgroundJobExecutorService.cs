using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.Jobs;

public class BackgroundJobExecutorService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    ILogger<BackgroundJobExecutorService> logger)
    : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job executor started. WorkerId: {WorkerId}", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = optionsMonitor.CurrentValue;

            if (options.Enabled)
            {
                try
                {
                    await ProcessTick(options, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background job executor tick failed");
                }
            }

            var delaySeconds = Math.Max(2, options.ExecutorPollSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task ProcessTick(BackgroundJobsOptions options, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await RecoverExpiredLocks(now, cancellationToken);
        var runIds = await ClaimRuns(now, Math.Max(1, options.ExecutorBatchSize), cancellationToken);
        if (runIds.Count == 0) return;

        foreach (var runId in runIds)
        {
            await ExecuteClaimedRun(runId, cancellationToken);
        }
    }

    private async Task RecoverExpiredLocks(DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LarchikContext>();

        var expiredRuns = await context.JobRuns
            .Include(x => x.JobDefinition)
            .Where(x =>
                x.Status == JobRunStatus.Running &&
                x.LockedUntilAt != null &&
                x.LockedUntilAt < now)
            .ToListAsync(cancellationToken);

        if (expiredRuns.Count == 0) return;

        foreach (var run in expiredRuns)
        {
            logger.LogWarning(
                "Run {RunId} lock expired for job {JobType}; scheduling retry/failover",
                run.Id,
                run.JobDefinition?.JobType ?? "unknown");

            run.Attempt += 1;
            run.LastError = TrimError("Job lock timeout expired");
            run.LockedBy = null;
            run.LockedUntilAt = null;
            run.UpdatedAt = now;

            if (run.Attempt >= run.MaxAttempts)
            {
                run.Status = JobRunStatus.Failed;
                run.CompletedAt = now;
            }
            else
            {
                var retryDelay = Math.Max(1, run.JobDefinition?.RetryDelayMinutes ?? 5);
                run.Status = JobRunStatus.RetryScheduled;
                run.AvailableAt = now.AddMinutes(retryDelay);
            }

            if (run.JobDefinition is not null)
            {
                run.JobDefinition.LastRunAt = now;
                run.JobDefinition.UpdatedAt = now;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Guid>> ClaimRuns(DateTime now, int batchSize, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LarchikContext>();

        var candidates = await context.JobRuns
            .Where(x =>
                (x.Status == JobRunStatus.Pending || x.Status == JobRunStatus.RetryScheduled) &&
                x.AvailableAt <= now &&
                (x.LockedUntilAt == null || x.LockedUntilAt < now) &&
                x.JobDefinition != null &&
                x.JobDefinition.IsEnabled)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.AvailableAt,
                LockTimeoutMinutes = x.JobDefinition!.LockTimeoutMinutes
            })
            .OrderBy(x => x.AvailableAt)
            .ThenBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return [];

        var claimedIds = new List<Guid>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var lockTimeout = Math.Max(1, candidate.LockTimeoutMinutes);
            var lockUntil = now.AddMinutes(lockTimeout);

            var affected = await context.JobRuns
                .Where(x =>
                    x.Id == candidate.Id &&
                    (x.Status == JobRunStatus.Pending || x.Status == JobRunStatus.RetryScheduled) &&
                    x.AvailableAt <= now &&
                    (x.LockedUntilAt == null || x.LockedUntilAt < now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, JobRunStatus.Running)
                    .SetProperty(x => x.StartedAt, now)
                    .SetProperty(x => x.LockedBy, _workerId)
                    .SetProperty(x => x.LockedUntilAt, lockUntil)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken);

            if (affected == 1)
            {
                claimedIds.Add(candidate.Id);
            }
        }

        return claimedIds;
    }

    private async Task ExecuteClaimedRun(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LarchikContext>();

        var handlers = scope.ServiceProvider
            .GetServices<IBackgroundJobHandler>()
            .ToDictionary(x => x.JobType, x => x, StringComparer.OrdinalIgnoreCase);

        var run = await context.JobRuns
            .Include(x => x.JobDefinition)
            .FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);

        if (run is null ||
            run.Status != JobRunStatus.Running ||
            !string.Equals(run.LockedBy, _workerId, StringComparison.Ordinal))
        {
            return;
        }

        var now = DateTime.UtcNow;
        JobExecutionResult result;

        if (run.JobDefinition is null)
        {
            result = JobExecutionResult.Failure("Missing job definition");
        }
        else if (!handlers.TryGetValue(run.JobDefinition.JobType, out var handler))
        {
            result = JobExecutionResult.Failure($"No handler registered for '{run.JobDefinition.JobType}'");
        }
        else
        {
            try
            {
                logger.LogInformation(
                    "Starting run {RunId} for job {JobName} ({JobType}), attempt {Attempt}/{MaxAttempts}",
                    run.Id,
                    run.JobDefinition.Name,
                    run.JobDefinition.JobType,
                    run.Attempt + 1,
                    run.MaxAttempts);

                result = await handler.ExecuteAsync(run.PayloadJson, cancellationToken);
            }
            catch (Exception ex)
            {
                result = JobExecutionResult.Failure(ex.Message);
                logger.LogError(ex, "Background job {JobType} execution failed", run.JobDefinition.JobType);
            }
        }

        run.Attempt += 1;
        run.LockedBy = null;
        run.LockedUntilAt = null;
        run.UpdatedAt = now;

        if (result.IsSuccess)
        {
            run.Status = JobRunStatus.Succeeded;
            run.CompletedAt = now;
            run.LastError = null;

            logger.LogInformation(
                "Run {RunId} for job {JobName} ({JobType}) succeeded on attempt {Attempt}",
                run.Id,
                run.JobDefinition?.Name ?? "unknown",
                run.JobDefinition?.JobType ?? "unknown",
                run.Attempt);
        }
        else
        {
            run.LastError = TrimError(result.Error);
            if (run.Attempt >= run.MaxAttempts)
            {
                run.Status = JobRunStatus.Failed;
                run.CompletedAt = now;

                logger.LogError(
                    "Run {RunId} for job {JobName} ({JobType}) failed permanently after {Attempt} attempts. Error: {Error}",
                    run.Id,
                    run.JobDefinition?.Name ?? "unknown",
                    run.JobDefinition?.JobType ?? "unknown",
                    run.Attempt,
                    run.LastError);
            }
            else
            {
                var retryDelay = Math.Max(1, run.JobDefinition?.RetryDelayMinutes ?? 5);
                run.Status = JobRunStatus.RetryScheduled;
                run.AvailableAt = now.AddMinutes(retryDelay);

                logger.LogError(
                    "Run {RunId} for job {JobName} ({JobType}) failed on attempt {Attempt}. Retry at {RetryAtUtc:O} UTC. Error: {Error}",
                    run.Id,
                    run.JobDefinition?.Name ?? "unknown",
                    run.JobDefinition?.JobType ?? "unknown",
                    run.Attempt,
                    run.AvailableAt,
                    run.LastError);
            }
        }

        if (run.JobDefinition is not null)
        {
            run.JobDefinition.LastRunAt = now;
            run.JobDefinition.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? TrimError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return null;
        return error.Length <= 4000 ? error : error[..4000];
    }
}
