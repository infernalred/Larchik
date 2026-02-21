namespace Larchik.Persistence.Entities;

public class JobRun
{
    public Guid Id { get; set; }
    public Guid JobDefinitionId { get; set; }
    public string DedupKey { get; set; } = null!;
    public string PayloadJson { get; set; } = "{}";
    public JobRunStatus Status { get; set; } = JobRunStatus.Pending;
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public string? LockedBy { get; set; }
    public DateTime? LockedUntilAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public JobDefinition? JobDefinition { get; set; }
}
