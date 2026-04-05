using Larchik.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Larchik.Infrastructure.Recalculation;

public class NoOpPortfolioRecalcService(ILogger<NoOpPortfolioRecalcService> logger) : IPortfolioRecalcService
{
    public Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Recalc requested for portfolio {PortfolioId} from {FromDate}", portfolioId, fromDate);
        return Task.CompletedTask;
    }
}
