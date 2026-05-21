namespace Bit.Core.PrivilegedAccessManagement.Models.Policies;

/// <summary>
/// Composite policy that approves only when every child policy approves.
/// </summary>
public sealed class AllOfPolicy : LeasingPolicy
{
    public IReadOnlyList<LeasingPolicy> Policies { get; init; } = [];
}
