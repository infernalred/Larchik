using System.Text;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Larchik.Infrastructure.MarketDataImports;

public sealed class MarketDataImportOutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<RabbitMqOptions> optionsMonitor,
    ILogger<MarketDataImportOutboxPublisherService> logger) : BackgroundService
{
    private readonly string instanceId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!optionsMonitor.CurrentValue.Enabled)
        {
            logger.LogInformation("RabbitMQ market data outbox publisher is disabled");
            return;
        }

        logger.LogInformation("RabbitMQ market data outbox publisher started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishBatchAsync(optionsMonitor.CurrentValue, stoppingToken);
                if (published > 0)
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ outbox publish cycle failed");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Clamp(optionsMonitor.CurrentValue.OutboxPollSeconds, 1, 60)),
                stoppingToken);
        }
    }

    private async Task<int> PublishBatchAsync(RabbitMqOptions options, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LarchikContext>();
        var now = DateTime.UtcNow;
        var messages = await context.OutboxMessages
            .Where(x => x.PublishedAt == null && x.AvailableAt <= now &&
                        (x.LockedUntilAt == null || x.LockedUntilAt < now))
            .OrderBy(x => x.OccurredAt)
            .Take(Math.Clamp(options.OutboxBatchSize, 1, 200))
            .ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            message.LockedBy = instanceId;
            message.LockedUntilAt = now.AddMinutes(2);
            message.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        var published = 0;
        try
        {
            var factory = RabbitMqTopology.CreateConnectionFactory(options);
            await using var connection = await factory.CreateConnectionAsync("larchik-market-data-outbox", cancellationToken);
            await using var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            await RabbitMqTopology.DeclareAsync(channel, options, cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    var properties = new BasicProperties
                    {
                        Persistent = true,
                        ContentType = "application/json",
                        Type = message.MessageType,
                        MessageId = message.Id.ToString("D"),
                        Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt).ToUnixTimeSeconds())
                    };
                    await channel.BasicPublishAsync(
                        options.Exchange,
                        options.RoutingKey,
                        mandatory: true,
                        properties,
                        Encoding.UTF8.GetBytes(message.PayloadJson),
                        cancellationToken);

                    message.PublishedAt = DateTime.UtcNow;
                    message.AttemptCount += 1;
                    message.LastError = null;
                    message.LockedBy = null;
                    message.LockedUntilAt = null;
                    message.UpdatedAt = message.PublishedAt.Value;
                    published++;
                }
                catch (Exception ex)
                {
                    Release(message, options, ex.Message);
                    logger.LogWarning(ex, "Could not publish outbox message {MessageId}", message.Id);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            foreach (var message in messages.Where(x => x.PublishedAt is null && x.LockedBy == instanceId))
            {
                Release(message, options, ex.Message);
            }

            await context.SaveChangesAsync(cancellationToken);
            throw;
        }

        await context.SaveChangesAsync(cancellationToken);
        return published;
    }

    private static void Release(OutboxMessage message, RabbitMqOptions options, string error)
    {
        var now = DateTime.UtcNow;
        message.AttemptCount += 1;
        message.LastError = Trim(error);
        message.LockedBy = null;
        message.LockedUntilAt = null;
        message.AvailableAt = now.AddSeconds(Math.Clamp(options.RetryDelaySeconds, 1, 3600));
        message.UpdatedAt = now;
    }

    private static string Trim(string value) => value.Length <= 4000 ? value : value[..4000];
}
