using System.Text.Json;
using Larchik.Application.FxRates.SyncCbrFxRates;
using MediatR;

namespace Larchik.Infrastructure.Jobs;

public class FxCbrDailyJobHandler(IMediator mediator) : IBackgroundJobHandler
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
                return JobExecutionResult.Failure($"Invalid payload: {ex.Message}");
            }
        }

        var result = await mediator.Send(new SyncCbrFxRatesCommand(date), cancellationToken);
        return result.IsSuccess
            ? JobExecutionResult.Success()
            : JobExecutionResult.Failure(result.Error ?? "FX sync failed");
    }
}
