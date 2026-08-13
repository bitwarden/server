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
/// { "kind": "time_of_day", "tz": "America/New_York",
///   "windows": [{ "days": ["mon", "tue"], "from": "09:00", "to": "17:00" }] }
/// </code>
/// </remarks>
public sealed class TimeOfDayCondition : AccessCondition
{
    /// <summary>The IANA timezone the windows are expressed in (e.g. <c>"America/New_York"</c>). Required.</summary>
    public string Tz { get; init; } = string.Empty;

    /// <summary>
    /// The windows access is permitted in. The condition allows when the request falls inside any one of them.
    /// At least one required; an empty list denies.
    /// </summary>
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];

    public override AccessEvaluation Evaluate(AccessSignals signals)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(Tz, out var timeZone))
        {
            // No window can be evaluated without a resolvable timezone, so fail closed.
            return AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow);
        }

        var local = TimeZoneInfo.ConvertTime(signals.Timestamp, timeZone);
        var day = local.DayOfWeek;
        var time = TimeOnly.FromTimeSpan(local.TimeOfDay);

        return Windows.Any(window => Contains(window, day, time))
            ? AccessEvaluation.Allow
            : AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow);
    }

    public override AccessRuleValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Tz))
        {
            return AccessRuleValidationResult.Invalid("time_of_day requires a tz.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(Tz, out _))
        {
            return AccessRuleValidationResult.Invalid($"Unknown timezone: '{Tz}'.");
        }

        if (Windows.Count == 0)
        {
            return AccessRuleValidationResult.Invalid("time_of_day requires at least one window.");
        }

        foreach (var window in Windows)
        {
            if (window.Days.Count == 0)
            {
                return AccessRuleValidationResult.Invalid("time_of_day window requires at least one day.");
            }

            // Day tokens are validated during deserialization by AccessWeekdayJsonConverter; an unknown token fails
            // the JSON parse and is reported as malformed before reaching here.
            if (!TryParseTime(window.From, out _))
            {
                return AccessRuleValidationResult.Invalid($"Invalid 'from' time: '{window.From}'. Expected HH:mm.");
            }

            if (!TryParseTime(window.To, out _))
            {
                return AccessRuleValidationResult.Invalid($"Invalid 'to' time: '{window.To}'. Expected HH:mm.");
            }
        }

        return AccessRuleValidationResult.Valid;
    }

    private static bool Contains(TimeWindow window, DayOfWeek day, TimeOnly time)
    {
        // AccessWeekday values align with System.DayOfWeek (Sunday = 0), so a direct cast compares correctly.
        if (!window.Days.Any(d => (DayOfWeek)d == day))
        {
            return false;
        }

        return TryParseTime(window.From, out var from)
            && TryParseTime(window.To, out var to)
            && time >= from && time <= to;
    }

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}

public sealed class TimeWindow
{
    public IReadOnlyList<AccessWeekday> Days { get; init; } = [];
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
}
