using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public class MoexPricesDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.MoexPricesDailyJobType;

    public IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow) =>
        DailyJobRunPlannerHelper.BuildTodayAndYesterdayRuns("prices:moex", utcNow);
}
