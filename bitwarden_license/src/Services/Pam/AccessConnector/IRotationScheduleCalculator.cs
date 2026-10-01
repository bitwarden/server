namespace Bit.Services.Pam.AccessConnector;

/// <summary>
/// Computes and validates a rotation config's Quartz 6-field cron schedule (spec <c>NextScheduledTime</c>). All
/// times are UTC.
/// </summary>
public interface IRotationScheduleCalculator
{
    /// <summary>
    /// Returns the next occurrence of <paramref name="cron"/> strictly after <paramref name="afterUtc"/>, or null
    /// for a null <paramref name="cron"/> (no scheduled rotation). Throws
    /// <see cref="Bit.Core.Exceptions.BadRequestException"/> for an unparseable cron expression.
    /// </summary>
    DateTime? GetNextOccurrence(string? cron, DateTime afterUtc);

    /// <summary>
    /// Validates that <paramref name="cron"/> is parseable and that the gap between its next two occurrences is at
    /// least <paramref name="minInterval"/> (the abuse floor). A null <paramref name="cron"/> is always valid,
    /// meaning no scheduled rotation; otherwise throws <see cref="Bit.Core.Exceptions.BadRequestException"/>.
    /// </summary>
    void ValidateSchedule(string? cron, TimeSpan minInterval);
}
