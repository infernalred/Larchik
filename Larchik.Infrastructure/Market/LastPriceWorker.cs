using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Larchik.Infrastructure.Market;

public class LastPriceWorker : BackgroundService
{
    private readonly ILogger<LastPriceWorker> _logger;

    public LastPriceWorker(ILogger<LastPriceWorker> logger, IServiceProvider services)
    {
        _logger = logger;
        Services = services;
    }

    private IServiceProvider Services { get; }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = Services.CreateScope();
            
            var lastPriceUpdaterService = scope.ServiceProvider
                        .GetRequiredService<LastPriceUpdater>();

            try
            {
                await lastPriceUpdaterService.UpdateLastPrice(stoppingToken);
            }
            catch (Exception e)
            {
               _logger.LogError("Error occurred executing {service}. Message: {message}. Error: {error}", nameof(lastPriceUpdaterService), e.Message, e);
            }
            
            
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(3600000, stoppingToken);
        }
    }
}