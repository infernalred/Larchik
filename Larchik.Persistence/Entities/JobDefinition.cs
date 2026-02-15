namespace Larchik.Persistence.Entities;

public class JobDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string JobType { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public JobScheduleType ScheduleType { get; set; } = JobScheduleType.DailyUtc;
    public string ScheduleValue { get; set; } = "03:00";
    public DateTime? LastRunAt { get; set; }
    public DateTime NextRunAt { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public int RetryDelayMinutes { get; set; } = 15;
    public int LockTimeoutMinutes { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<JobRun> Runs { get; set; } = new List<JobRun>();
}
