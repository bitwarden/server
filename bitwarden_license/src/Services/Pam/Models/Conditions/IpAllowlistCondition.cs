namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Auto-approves a lease when the requester's IP matches a listed CIDR; otherwise denies.
/// </summary>
public sealed class IpAllowlistCondition : AccessCondition
{
    /// <summary>
    /// The allowed source ranges in CIDR notation (e.g. <c>"10.0.0.0/8"</c>). The condition allows when the caller's
    /// IP is in any one of them. At least one required, and each must parse; an empty list denies.
    /// </summary>
    public IReadOnlyList<string> Cidrs { get; init; } = [];
}
