using System.Text.Json;
using Larchik.Application.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.Jobs;

public sealed class PortfolioReconciliationDailyJobHandler(
    IPortfolioReconciliationReportService reportService,
    IPortfolioRecalcService recalcService,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    ILogger<PortfolioReconciliationDailyJobHandler> logger)
    : IBackgroundJobHandler
{
    public string JobType => BackgroundJobConstants.PortfolioReconciliationDailyJobType;

    public async Task<JobExecutionResult> ExecuteAsync(string payloadJson, CancellationToken cancellationToken)
    {
        DateOnly? date = null;

        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                if ((doc.RootElement.TryGetProperty("date", out var dateElement) ||
                     doc.RootElement.TryGetProperty("Date", out dateElement)) &&
                    DateOnly.TryParse(dateElement.GetString(), out var parsed))
                {
                    date = parsed;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reconciliation daily job received invalid payload: {Payload}", payloadJson);
                return JobExecutionResult.Failure($"Invalid payload: {ex.Message}");
            }
        }

        var runDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var options = optionsMonitor.CurrentValue.PortfolioReconciliationDaily;
        var runDateUtc = DateTime.SpecifyKind(runDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var targetIds = options.Targets
            .Where(x => ReconciliationTargetDateHelper.ShouldIncludeTarget(x, runDate))
            .Select(x => x.PortfolioId)
            .Distinct()
            .ToArray();

        foreach (var portfolioId in targetIds)
        {
            await recalcService.ScheduleRebuild(portfolioId, runDateUtc, cancellationToken);
        }

        await reportService.LogDailyReportAsync(runDate, source: BackgroundJobConstants.PortfolioReconciliationDailyJobType, cancellationToken);
        logger.LogInformation(
            "Reconciliation daily job completed for {Date} UTC. Recalculated portfolios: {PortfolioCount}",
            runDate.ToString("yyyy-MM-dd"),
            targetIds.Length);
        return JobExecutionResult.Success();
    }
}
