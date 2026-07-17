using Bit.Services.Pam.Enums;
namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Auto-approves a lease when the request falls inside one of the configured windows, evaluated in
/// the named IANA timezone; otherwise denies.
/// </summary>
public sealed class TimeOfDayCondition : AccessCondition
{
    /// <summary>The IANA timezone (e.g. <c>"America/New_York"</c>) the windows are evaluated in. An unknown or invalid zone denies.</summary>
    public string Tz { get; init; } = string.Empty;

    /// <summary>The windows that grant access; the condition allows when the instant falls in any one of them. At least one required.</summary>
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];
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
}
