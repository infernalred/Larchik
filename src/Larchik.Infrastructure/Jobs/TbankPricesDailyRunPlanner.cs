using System.Text.Json;
using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public class TbankPricesDailyRunPlanner : IJobRunPlanner
{
    public string JobType => BackgroundJobConstants.TbankPricesDailyJobType;

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
        var dedupKey = $"prices:tbank:{date:yyyy-MM-dd}";
        return new ScheduledRunSpec(dedupKey, payload, utcNow);
    }
}
