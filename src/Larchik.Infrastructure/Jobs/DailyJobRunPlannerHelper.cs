using System.Text.Json;

namespace Larchik.Infrastructure.Jobs;

internal static class DailyJobRunPlannerHelper
{
    public static IReadOnlyCollection<ScheduledRunSpec> BuildTodayAndYesterdayRuns(
        string dedupPrefix,
        DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow.Date);
        var yesterday = today.AddDays(-1);

        return
        [
            CreateRun(dedupPrefix, today, utcNow),
            CreateRun(dedupPrefix, yesterday, utcNow)
        ];
    }

    public static ScheduledRunSpec CreateRun(string dedupPrefix, DateOnly date, DateTime utcNow)
    {
        var payload = JsonSerializer.Serialize(new { date = date.ToString("yyyy-MM-dd") });
        var dedupKey = $"{dedupPrefix}:{date:yyyy-MM-dd}";
        return new ScheduledRunSpec(dedupKey, payload, utcNow);
    }
}
