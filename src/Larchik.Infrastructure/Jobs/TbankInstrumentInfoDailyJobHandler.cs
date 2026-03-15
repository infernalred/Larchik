using Larchik.Application.Stocks.SyncTbankInstrumentInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.Jobs;

public class TbankInstrumentInfoDailyJobHandler(
    SyncTbankInstrumentInfoCommandHandler syncHandler,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    ILogger<TbankInstrumentInfoDailyJobHandler> logger)
    : IBackgroundJobHandler
{
    public string JobType => BackgroundJobConstants.TbankInstrumentInfoDailyJobType;

    public async Task<JobExecutionResult> ExecuteAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue.TbankInstrumentInfoDaily;
        var result = await syncHandler.Handle(
            new SyncTbankInstrumentInfoCommand(
                options.BaseUrl,
                options.Token,
                options.AllowInvalidTls,
                options.CountryExclusions,
                options.MaxParallelism),
            cancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "TBANK instrument info daily job completed. Saved DB changes: {Changes}",
                result.Value);
        }

        return result.IsSuccess
            ? JobExecutionResult.Success()
            : JobExecutionResult.Failure(result.Error ?? "TBANK instrument info sync failed");
    }
}
