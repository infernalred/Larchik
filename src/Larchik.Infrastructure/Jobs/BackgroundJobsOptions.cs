namespace Larchik.Infrastructure.Jobs;

public class BackgroundJobsOptions
{
    public bool Enabled { get; set; } = true;
    public int SchedulerPollSeconds { get; set; } = 30;
    public int ExecutorPollSeconds { get; set; } = 10;
    public int ExecutorBatchSize { get; set; } = 5;
    public FxCbrDailyJobOptions FxCbrDaily { get; set; } = new();
    public MoexPricesDailyJobOptions MoexPricesDaily { get; set; } = new();
}

public class FxCbrDailyJobOptions
{
    public bool Enabled { get; set; } = true;
    public int HourUtc { get; set; } = 5;
    public int MinuteUtc { get; set; } = 10;
    public int MaxAttempts { get; set; } = 8;
    public int RetryDelayMinutes { get; set; } = 20;
    public int LockTimeoutMinutes { get; set; } = 5;
}

public class MoexPricesDailyJobOptions
{
    public bool Enabled { get; set; } = true;
    public int HourUtc { get; set; } = 19;
    public int MinuteUtc { get; set; } = 20;
    public int MaxAttempts { get; set; } = 8;
    public int RetryDelayMinutes { get; set; } = 20;
    public int LockTimeoutMinutes { get; set; } = 10;
    public string Provider { get; set; } = "MOEX";
    public string BaseUrl { get; set; } = "https://iss.moex.com/iss";
    public string[] Boards { get; set; } = ["TQBR", "TQTF", "TQIF", "TQCB", "TQOB"];
}
