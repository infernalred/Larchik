using System.Text.Json;
using Larchik.Application.FxRates.SyncCbrFxRates;
using Microsoft.Extensions.Logging;

namespace Larchik.Infrastructure.Jobs;

public class FxCbrDailyJobHandler(
    SyncCbrFxRatesCommandHandler syncHandler,
    ILogger<FxCbrDailyJobHandler> logger) : IBackgroundJobHandler
{
    public string JobType => BackgroundJobConstants.FxCbrDailyJobType;

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
                logger.LogError(ex, "CBR FX daily job received invalid payload: {Payload}", payloadJson);
                return JobExecutionResult.Failure($"Invalid payload: {ex.Message}");
            }
        }

        var result = await syncHandler.Handle(new SyncCbrFxRatesCommand(date), cancellationToken);
        if (result.IsSuccess)
        {
            logger.LogInformation(
                "CBR FX daily job completed for {Date} UTC. Saved DB changes: {Changes}",
                (date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date)).ToString("yyyy-MM-dd"),
                result.Value);
        }
        else
        {
            logger.LogError(
                "CBR FX daily job failed for {Date} UTC. Error: {Error}",
                (date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date)).ToString("yyyy-MM-dd"),
                result.Error ?? "FX sync failed");
        }

        return result.IsSuccess
            ? JobExecutionResult.Success()
            : JobExecutionResult.Failure(result.Error ?? "FX sync failed");
    }
}
