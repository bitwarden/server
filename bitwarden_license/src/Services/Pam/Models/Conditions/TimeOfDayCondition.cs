using Bit.Services.Pam.Enums;
namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Auto-approves a lease when the request falls inside one of the configured windows, evaluated in
/// the named IANA timezone; otherwise denies.
/// </summary>
public sealed class TimeOfDayCondition : AccessCondition
{
    public string Tz { get; init; } = string.Empty;
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];
}

public sealed class TimeWindow
{
    public IReadOnlyList<AccessWeekday> Days { get; init; } = [];
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
}
