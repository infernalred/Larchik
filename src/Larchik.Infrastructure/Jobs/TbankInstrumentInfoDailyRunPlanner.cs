using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public class TbankInstrumentInfoDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.TbankInstrumentInfoDailyJobType;

    public IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow.Date);
        var payload = $"{{\"date\":\"{today:yyyy-MM-dd}\"}}";
        var dedupKey = $"instrument-info:tbank:{today:yyyy-MM-dd}";
        return [new ScheduledRunSpec(dedupKey, payload, utcNow)];
    }
}
