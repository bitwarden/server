using System.Net;
using Bit.Services.Pam.Engine;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Auto-approves a lease when the requester's IP matches a listed CIDR; otherwise denies.
/// </summary>
/// <remarks>
/// Wire format:
/// <code>
/// { "kind": "ip_allowlist", "cidrs": ["10.0.0.0/8", "2001:db8::/32"] }
/// </code>
/// </remarks>
public sealed class IpAllowlistCondition : AccessCondition
{
    /// <summary>
    /// The allowed source ranges in CIDR notation (e.g. <c>"10.0.0.0/8"</c>). The condition allows when the caller's
    /// IP is in any one of them. At least one required, and each must parse; an empty list denies.
    /// </summary>
    public IReadOnlyList<string> Cidrs { get; init; } = [];

    public override AccessEvaluation Evaluate(AccessSignals signals)
    {
        // An allowlist with no entries permits no address; combined with an unknown caller IP, both fail closed.
        if (Cidrs.Count == 0 || signals.IpAddress is null)
        {
            return AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
        }

        foreach (var cidr in Cidrs)
        {
            if (IPNetwork.TryParse(cidr, out var network) && network.Contains(signals.IpAddress))
            {
                return AccessEvaluation.Allow;
            }
        }

        return AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
    }

    public override T Accept<T>(IAccessConditionVisitor<T> visitor) => visitor.VisitIpAllowlist(this);
}
