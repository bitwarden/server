using Bit.Core.Exceptions;
using Quartz;

namespace Bit.Services.Pam.AccessConnector;

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

        // A cron that fires fewer than twice cannot be checked against the floor, so it is rejected.
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
            // Quartz defaults to TimeZoneInfo.Local; schedules are UTC, so a day-anchored cron must not shift with
            // the host's time zone or DST.
            return new CronExpression(cron) { TimeZone = TimeZoneInfo.Utc };
        }
        catch (FormatException ex)
        {
            throw new BadRequestException($"The schedule is not a valid cron expression: {ex.Message}");
        }
    }
}
