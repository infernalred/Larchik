using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public interface IJobRunPlanner
{
    string JobType { get; }
    IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow);
}

public record ScheduledRunSpec(string DedupKey, string PayloadJson, DateTime AvailableAtUtc);
