namespace Larchik.Persistence.Entities;

public enum JobRunStatus
{
    Pending = 1,
    Running = 2,
    RetryScheduled = 3,
    Succeeded = 4,
    Failed = 5
}
