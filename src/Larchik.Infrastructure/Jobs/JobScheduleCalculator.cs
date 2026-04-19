using Larchik.Persistence.Entities;

namespace Larchik.Infrastructure.Jobs;

public static class JobScheduleCalculator
{
    private const string DefaultDailySchedule = "05:10";
    private const int DefaultFallbackIntervalMinutes = 5;
    private const int DefaultIntervalMinutes = 60;

    public static DateTime ComputeNextRunUtc(JobDefinition definition, DateTime utcNow)
    {
        return definition.ScheduleType switch
        {
            JobScheduleType.DailyUtc => ComputeDaily(definition.ScheduleValue, utcNow),
            JobScheduleType.IntervalMinutes => ComputeInterval(definition.ScheduleValue, utcNow),
            _ => utcNow.AddMinutes(DefaultFallbackIntervalMinutes)
        };
    }

    private static DateTime ComputeDaily(string scheduleValue, DateTime utcNow)
    {
        var (hour, minute) = TryParseTime(scheduleValue, out var parsedHour, out var parsedMinute)
            ? (parsedHour, parsedMinute)
            : ParseDefaultDailyTime();

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
            minutes = DefaultIntervalMinutes;
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

    private static (int Hour, int Minute) ParseDefaultDailyTime()
    {
        TryParseTime(DefaultDailySchedule, out var hour, out var minute);
        return (hour, minute);
    }
}
