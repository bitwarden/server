using System.Globalization;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Auto-approves a lease when the request falls inside one of the configured windows, evaluated in
/// the named IANA timezone; otherwise denies.
/// </summary>
/// <remarks>
/// Wire format:
/// <code>
/// {
///   "kind": "time_of_day",
///   "tz": "America/New_York",
///   "windows": [{ "days": ["mon", "tue", "wed", "thu", "fri"], "from": "09:00", "to": "17:00" }]
/// }
/// </code>
/// </remarks>
public sealed class TimeOfDayCondition : AccessCondition
{
    /// <summary>The IANA timezone (e.g. <c>"America/New_York"</c>) the windows are evaluated in. An unknown or invalid zone denies.</summary>
    public string Tz { get; init; } = string.Empty;

    /// <summary>The windows that grant access; the condition allows when the instant falls in any one of them. At least one required.</summary>
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];

    public override AccessEvaluation Evaluate(AccessSignals signals)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(Tz, out var timeZone))
        {
            // The window cannot be evaluated without a valid timezone, so fail closed.
            return AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow);
        }

        var local = TimeZoneInfo.ConvertTime(signals.Timestamp, timeZone);
        var day = local.DayOfWeek;
        var time = TimeOnly.FromTimeSpan(local.TimeOfDay);

        return Windows.Any(window => window.Contains(day, time)) ? AccessEvaluation.Allow : AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow);
    }

    public override T Accept<T>(IAccessConditionVisitor<T> visitor) => visitor.VisitTimeOfDay(this);
}

/// <summary>
/// A single recurring window: the days it applies on and the daily start/end times, all in the parent
/// condition's timezone.
/// </summary>
public sealed class TimeWindow
{
    /// <summary>The days of the week the window is active on. At least one required.</summary>
    public IReadOnlyList<AccessWeekday> Days { get; init; } = [];

    /// <summary>Window start, 24-hour <c>HH:mm</c> (<c>00:00</c>–<c>23:59</c>). Inclusive.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>Window end, 24-hour <c>HH:mm</c> (<c>00:00</c>–<c>23:59</c>). Inclusive.</summary>
    public string To { get; init; } = string.Empty;

    /// <summary>
    /// Whether this window admits the given <paramref name="day"/> and <paramref name="time"/> (both already in the
    /// parent condition's timezone). Unparseable bounds admit nothing, so the window fails closed.
    /// </summary>
    public bool Contains(DayOfWeek day, TimeOnly time)
    {
        // AccessWeekday values align with System.DayOfWeek (Sunday = 0), so a direct cast compares correctly.
        if (Days.All(d => (DayOfWeek)d != day))
        {
            return false;
        }

        return TimeOnly.TryParseExact(From, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            && TimeOnly.TryParseExact(To, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
            && time >= from && time <= to;
    }
}
