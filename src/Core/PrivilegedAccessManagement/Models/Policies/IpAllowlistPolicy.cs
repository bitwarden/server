namespace Bit.Core.PrivilegedAccessManagement.Models.Policies;

/// <summary>
/// Auto-approves a lease when the requester's IP matches a listed CIDR; otherwise denies.
/// </summary>
public sealed class IpAllowlistPolicy : LeasingPolicy
{
    public IReadOnlyList<string> Cidrs { get; init; } = [];
}
