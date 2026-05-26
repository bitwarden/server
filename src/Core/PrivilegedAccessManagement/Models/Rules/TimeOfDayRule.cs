namespace Bit.Core.PrivilegedAccessManagement.Models.Rules;

/// <summary>
/// Auto-approves a lease when the request falls inside one of the configured windows, evaluated in
/// the named IANA timezone; otherwise denies.
/// </summary>
public sealed class TimeOfDayRule : Rule
{
    public string Tz { get; init; } = string.Empty;
    public IReadOnlyList<TimeWindow> Windows { get; init; } = [];
}

public sealed class TimeWindow
{
    public IReadOnlyList<string> Days { get; init; } = [];
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
}
