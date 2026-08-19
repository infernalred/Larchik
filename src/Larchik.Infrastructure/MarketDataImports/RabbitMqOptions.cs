namespace Larchik.Infrastructure.MarketDataImports;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; set; } = true;
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "larchik.market-data";
    public string Queue { get; set; } = "larchik.market-data.import";
    public string RetryQueue { get; set; } = "larchik.market-data.import.retry";
    public string DeadLetterQueue { get; set; } = "larchik.market-data.import.dead";
    public string RoutingKey { get; set; } = "market-data.import";
    public string RetryRoutingKey { get; set; } = "market-data.import.retry";
    public string DeadLetterRoutingKey { get; set; } = "market-data.import.dead";
    public int RetryDelaySeconds { get; set; } = 60;
    public int OutboxPollSeconds { get; set; } = 2;
    public int OutboxBatchSize { get; set; } = 20;
    public int PrefetchCount { get; set; } = 1;
    public int ReconnectDelaySeconds { get; set; } = 5;
}
