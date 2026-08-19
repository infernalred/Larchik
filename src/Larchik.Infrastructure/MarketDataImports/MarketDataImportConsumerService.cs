using System.Text;
using System.Text.Json;
using Larchik.Application.MarketDataImports;
using Larchik.Application.MarketDataImports.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Larchik.Infrastructure.MarketDataImports;

public sealed class MarketDataImportConsumerService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<RabbitMqOptions> optionsMonitor,
    ILogger<MarketDataImportConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!optionsMonitor.CurrentValue.Enabled)
        {
            logger.LogInformation("RabbitMQ market data import consumer is disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(optionsMonitor.CurrentValue, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ market data consumer stopped; reconnecting");
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(optionsMonitor.CurrentValue.ReconnectDelaySeconds, 1, 60)),
                    stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(RabbitMqOptions options, CancellationToken cancellationToken)
    {
        var factory = RabbitMqTopology.CreateConnectionFactory(options);
        await using var connection = await factory.CreateConnectionAsync("larchik-market-data-consumer", cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, options, cancellationToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: (ushort)Math.Clamp(options.PrefetchCount, 1, 100),
            global: false,
            cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
            await HandleDeliveryAsync(channel, options, delivery, cancellationToken);
        await channel.BasicConsumeAsync(options.Queue, autoAck: false, consumer, cancellationToken);
        logger.LogInformation("RabbitMQ market data consumer started on queue {Queue}", options.Queue);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        RabbitMqOptions options,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        var body = delivery.Body.ToArray();
        try
        {
            var message = JsonSerializer.Deserialize<MarketDataImportMessage>(body);
            if (message is null || message.SchemaVersion != 1 || message.RequestId == Guid.Empty)
            {
                logger.LogWarning("Rejecting invalid market data message {MessageId}", delivery.BasicProperties.MessageId);
                await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessMarketDataImportCommandHandler>();
            var result = await handler.Handle(message.RequestId, cancellationToken);
            switch (result.Outcome)
            {
                case MarketDataImportProcessOutcome.Retry:
                    await PublishAsync(channel, options, options.RetryRoutingKey, body, delivery, cancellationToken);
                    break;
                case MarketDataImportProcessOutcome.Failed:
                    await PublishAsync(channel, options, options.DeadLetterRoutingKey, body, delivery, cancellationToken);
                    logger.LogError(
                        "Market data import {RequestId} failed permanently: {Error}",
                        message.RequestId,
                        result.Error);
                    break;
                case MarketDataImportProcessOutcome.Completed:
                case MarketDataImportProcessOutcome.Continue:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Outcome), result.Outcome, null);
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled market data consumer error for message {MessageId}", delivery.BasicProperties.MessageId);
            try
            {
                await PublishAsync(channel, options, options.RetryRoutingKey, body, delivery, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
            catch
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                throw;
            }
        }
    }

    private static Task PublishAsync(
        IChannel channel,
        RabbitMqOptions options,
        string routingKey,
        byte[] body,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = delivery.BasicProperties.ContentType ?? "application/json",
            Type = delivery.BasicProperties.Type,
            MessageId = delivery.BasicProperties.MessageId,
            CorrelationId = delivery.BasicProperties.CorrelationId
        };
        return channel.BasicPublishAsync(
            options.Exchange,
            routingKey,
            mandatory: true,
            properties,
            new ReadOnlyMemory<byte>(body),
            cancellationToken).AsTask();
    }
}
