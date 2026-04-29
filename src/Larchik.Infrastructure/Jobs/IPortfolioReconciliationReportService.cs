namespace Larchik.Infrastructure.Jobs;

public interface IPortfolioReconciliationReportService
{
    Task LogDailyReportAsync(DateOnly runDate, string source, CancellationToken cancellationToken);
}
