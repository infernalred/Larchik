using RabbitMQ.Client;

namespace Larchik.Infrastructure.MarketDataImports;

internal static class RabbitMqTopology
{
    public static ConnectionFactory CreateConnectionFactory(RabbitMqOptions options) => new()
    {
        HostName = options.HostName,
        Port = options.Port,
        UserName = options.UserName,
        Password = options.Password,
        VirtualHost = options.VirtualHost,
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(Math.Clamp(options.ReconnectDelaySeconds, 1, 300))
    };

    public static async Task DeclareAsync(IChannel channel, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-single-active-consumer"] = true,
                ["x-dead-letter-exchange"] = options.Exchange,
                ["x-dead-letter-routing-key"] = options.DeadLetterRoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.Queue,
            options.Exchange,
            options.RoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.RetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-message-ttl"] = Math.Clamp(options.RetryDelaySeconds, 1, 86_400) * 1000,
                ["x-dead-letter-exchange"] = options.Exchange,
                ["x-dead-letter-routing-key"] = options.RoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.RetryQueue,
            options.Exchange,
            options.RetryRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.DeadLetterQueue,
            options.Exchange,
            options.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }
}
