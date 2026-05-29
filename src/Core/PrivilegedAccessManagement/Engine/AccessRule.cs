using Bit.Core.Vault.Models.Data;

namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed record AccessRule
{
    public required string Name { get; init; }
    public required TimeSpan Duration { get; init; }

    public bool RequireSingleton { get; init; }
    public bool RequireApproval { get; init; }
    public List<string> RequiredCidr { get; init; } = [];

    public TimeOfDayConfig? TimeOfDay { get; init; }
}

public sealed record TimeOfDayConfig
{
    public required string TimeZone { get; init; } // IANA timezone id (e.g. "America/New_York")

    public required IReadOnlyList<AccessTimeWindow> Windows { get; init; }
}

public sealed record AccessTimeWindow
{
    public IReadOnlyList<DayOfWeek> Days { get; init; } = [];
    public required TimeOnly From { get; init; }
    public required TimeOnly To { get; init; }
}

public interface IAccessRuleResolver
{
    AccessRule? Resolve(CipherDetails cipher);
}
