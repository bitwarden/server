namespace Bit.Services.Pam.Rotation;

/// <summary>
/// Computes and validates a rotation config's Quartz 6-field cron schedule (spec <c>NextScheduledTime</c>). All
/// times are UTC.
/// </summary>
public interface IRotationScheduleCalculator
{
    /// <summary>
    /// Returns the next occurrence of <paramref name="cron"/> strictly after <paramref name="afterUtc"/>, or null
    /// when <paramref name="cron"/> is null (a config with no scheduled rotation). Throws
    /// <see cref="Bit.Core.Exceptions.BadRequestException"/> when <paramref name="cron"/> is not a parseable cron
    /// expression.
    /// </summary>
    DateTime? GetNextOccurrence(string? cron, DateTime afterUtc);

    /// <summary>
    /// Validates that <paramref name="cron"/> is parseable and that the gap between its next two occurrences is at
    /// least <paramref name="minInterval"/> (the abuse floor). A null <paramref name="cron"/> is always valid — it
    /// means no scheduled rotation. Throws <see cref="Bit.Core.Exceptions.BadRequestException"/> otherwise.
    /// </summary>
    void ValidateSchedule(string? cron, TimeSpan minInterval);
}
