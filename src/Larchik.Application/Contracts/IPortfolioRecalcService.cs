namespace Larchik.Application.Contracts;

public interface IPortfolioRecalcService
{
    Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default);
}
