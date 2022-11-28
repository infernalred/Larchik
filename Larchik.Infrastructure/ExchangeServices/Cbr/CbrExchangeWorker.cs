using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Larchik.Infrastructure.ExchangeServices.Cbr;

public class CbrExchangeWorker : BackgroundService
{
    private IServiceProvider Services { get; }
    private readonly ILogger<CbrExchangeWorker> _logger;

    public CbrExchangeWorker(ILogger<CbrExchangeWorker> logger, IServiceProvider services)
    {
        Services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = Services.CreateScope();
            
            var cbrExchangeService = scope.ServiceProvider
                .GetRequiredService<CbrExchangeRates>();

            try
            {
                await cbrExchangeService.GetLastRates(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred executing {service}. Message: {message}. Error: {error}", nameof(cbrExchangeService), e.Message, e);
            }
            
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(21600000, stoppingToken);
        }
    }
}
