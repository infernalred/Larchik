using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public class FxCbrDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.FxCbrDailyJobType;

    public IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow) =>
        DailyJobRunPlannerHelper.BuildTodayAndYesterdayRuns("fx:cbr", utcNow);
}
