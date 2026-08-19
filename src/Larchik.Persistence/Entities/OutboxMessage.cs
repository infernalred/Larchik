namespace Larchik.Persistence.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? LockedUntilAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
