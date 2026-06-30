namespace Bit.Commercial.Pam.Models.Conditions;

/// <summary>
/// Auto-approves a lease when the requester's IP matches a listed CIDR; otherwise denies.
/// </summary>
public sealed class IpAllowlistCondition : AccessCondition
{
    public IReadOnlyList<string> Cidrs { get; init; } = [];
}
