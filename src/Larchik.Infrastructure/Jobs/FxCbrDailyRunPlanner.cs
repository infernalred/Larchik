using System.Text.Json;
using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public class FxCbrDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.FxCbrDailyJobType;

    public IReadOnlyCollection<ScheduledRunSpec> BuildRuns(JobDefinition definition, DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow.Date);
        var yesterday = today.AddDays(-1);

        return
        [
            CreateRun(today, utcNow),
            CreateRun(yesterday, utcNow)
        ];
    }

    private static ScheduledRunSpec CreateRun(DateOnly date, DateTime utcNow)
    {
        var payload = JsonSerializer.Serialize(new { date = date.ToString("yyyy-MM-dd") });
        var dedupKey = $"fx:cbr:{date:yyyy-MM-dd}";
        return new ScheduledRunSpec(dedupKey, payload, utcNow);
    }
}
