using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public static class JobScheduleCalculator
{
    public static DateTime ComputeNextRunUtc(JobDefinition definition, DateTime utcNow)
    {
        return definition.ScheduleType switch
        {
            JobScheduleType.DailyUtc => ComputeDaily(definition.ScheduleValue, utcNow),
            JobScheduleType.IntervalMinutes => ComputeInterval(definition.ScheduleValue, utcNow),
            _ => utcNow.AddMinutes(5)
        };
    }

    private static DateTime ComputeDaily(string scheduleValue, DateTime utcNow)
    {
        if (!TryParseTime(scheduleValue, out var hour, out var minute))
        {
            hour = 5;
            minute = 10;
        }

        var candidate = new DateTime(
            utcNow.Year,
            utcNow.Month,
            utcNow.Day,
            hour,
            minute,
            0,
            DateTimeKind.Utc);

        return candidate > utcNow ? candidate : candidate.AddDays(1);
    }

    private static DateTime ComputeInterval(string scheduleValue, DateTime utcNow)
    {
        if (!int.TryParse(scheduleValue, out var minutes) || minutes <= 0)
        {
            minutes = 60;
        }

        return utcNow.AddMinutes(minutes);
    }

    private static bool TryParseTime(string value, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        var parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute)) return false;
        return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }
}
