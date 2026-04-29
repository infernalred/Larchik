using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public sealed class PortfolioReconciliationDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.PortfolioReconciliationDailyJobType;

    public IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow) =>
        DailyJobRunPlannerHelper.BuildTodayAndYesterdayRuns("reconciliation", utcNow);
}
