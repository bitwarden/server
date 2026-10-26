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
    /// Throws <see cref="Bit.Core.Exceptions.BadRequestException"/> unless <paramref name="cron"/> parses and its next
    /// two occurrences are at least <paramref name="minInterval"/> apart. A null cron, meaning no schedule, is valid.
    /// </summary>
    void ValidateSchedule(string? cron, TimeSpan minInterval);
}
