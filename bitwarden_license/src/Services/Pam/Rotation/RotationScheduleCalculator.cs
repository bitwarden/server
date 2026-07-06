using Bit.Core.Exceptions;
using Quartz;

namespace Bit.Services.Pam.Rotation;

/// <inheritdoc cref="IRotationScheduleCalculator" />
public class RotationScheduleCalculator : IRotationScheduleCalculator
{
    public DateTime? GetNextOccurrence(string? cron, DateTime afterUtc)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return null;
        }

        var expression = Parse(cron);
        var next = expression.GetNextValidTimeAfter(new DateTimeOffset(DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc)));
        return next?.UtcDateTime;
    }

    public void ValidateSchedule(string? cron, TimeSpan minInterval)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return;
        }

        var expression = Parse(cron);

        // The schedule must occur at least twice so the interval floor can be checked; a cron that fires only once
        // (or never) is rejected the same way a too-frequent one is.
        var first = expression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
        if (first is null)
        {
            throw new BadRequestException("The schedule does not occur.");
        }

        var second = expression.GetNextValidTimeAfter(first.Value);
        if (second is null)
        {
            throw new BadRequestException("The schedule does not occur.");
        }

        if (second.Value - first.Value < minInterval)
        {
            throw new BadRequestException(
                $"The schedule must run no more often than every {minInterval.TotalMinutes:0} minutes.");
        }
    }

    private static CronExpression Parse(string cron)
    {
        try
        {
            // Quartz evaluates crons in TimeZoneInfo.Local by default; schedules are contractually UTC
            // (a day-anchored cron must not shift with the server's deployment time zone or DST).
            return new CronExpression(cron) { TimeZone = TimeZoneInfo.Utc };
        }
        catch (FormatException ex)
        {
            throw new BadRequestException($"The schedule is not a valid cron expression: {ex.Message}");
        }
    }
}
