using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public class TbankInstrumentInfoDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.TbankInstrumentInfoDailyJobType;

    public IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow.Date);
        return [DailyJobRunPlannerHelper.CreateRun("instrument-info:tbank", today, utcNow)];
    }
}
