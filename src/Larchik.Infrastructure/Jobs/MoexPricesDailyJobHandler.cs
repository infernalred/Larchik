using System.Text.Json;
using Larchik.Application.Prices.SyncMoexPrices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.Jobs;

public class MoexPricesDailyJobHandler(
    SyncMoexPricesCommandHandler syncHandler,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    ILogger<MoexPricesDailyJobHandler> logger)
    : IBackgroundJobHandler
{
    public string JobType => BackgroundJobConstants.MoexPricesDailyJobType;

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
                return JobExecutionResult.Failure($"Invalid payload: {ex.Message}");
            }
        }

        var options = optionsMonitor.CurrentValue.MoexPricesDaily;
        var result = await syncHandler.Handle(
            new SyncMoexPricesCommand(
                date,
                options.Boards,
                options.Provider,
                options.BaseUrl),
            cancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "MOEX daily job completed for {Date} UTC. Saved DB changes: {Changes}",
                (date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date)).ToString("yyyy-MM-dd"),
                result.Value);
        }

        return result.IsSuccess
            ? JobExecutionResult.Success()
            : JobExecutionResult.Failure(result.Error ?? "MOEX price sync failed");
    }
}
