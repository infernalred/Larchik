using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public class TbankPricesDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.TbankPricesDailyJobType;

    public IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow) =>
        DailyJobRunPlannerHelper.BuildTodayAndYesterdayRuns("prices:tbank", utcNow);
}
