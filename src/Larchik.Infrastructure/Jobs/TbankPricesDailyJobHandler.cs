using System.Text.Json;
using Larchik.Application.Prices.SyncTbankPrices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.Jobs;

public class TbankPricesDailyJobHandler(
    SyncTbankPricesCommandHandler syncHandler,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    ILogger<TbankPricesDailyJobHandler> logger)
    : IBackgroundJobHandler
{
    public string JobType => BackgroundJobConstants.TbankPricesDailyJobType;

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
                logger.LogError(ex, "TBANK daily job received invalid payload: {Payload}", payloadJson);
                return JobExecutionResult.Failure($"Invalid payload: {ex.Message}");
            }
        }

        var options = optionsMonitor.CurrentValue.TbankPricesDaily;
        var result = await syncHandler.Handle(
            new SyncTbankPricesCommand(
                date,
                options.Provider,
                options.BaseUrl,
                options.Token,
                options.AllowInvalidTls,
                options.CountryExclusions,
                options.MaxHistoryLookbackDays,
                options.MaxParallelism),
            cancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "TBANK daily job completed for {Date} UTC. Saved DB changes: {Changes}",
                (date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date)).ToString("yyyy-MM-dd"),
                result.Value);
        }
        else
        {
            logger.LogError(
                "TBANK daily job failed for {Date} UTC. Error: {Error}",
                (date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date)).ToString("yyyy-MM-dd"),
                result.Error ?? "TBANK price sync failed");
        }

        return result.IsSuccess
            ? JobExecutionResult.Success()
            : JobExecutionResult.Failure(result.Error ?? "TBANK price sync failed");
    }
}
