using System.Net;
using Bit.Services.Pam.Engine;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>Auto-approves a lease when the requester's IP matches a listed CIDR; otherwise denies.</summary>
/// <remarks>
/// Wire format:
/// <code>
/// { "kind": "ip_allowlist", "cidrs": ["10.0.0.0/8", "2001:db8::/32"] }
/// </code>
/// </remarks>
public sealed class IpAllowlistCondition : AccessCondition
{
    private readonly IReadOnlyList<string> _cidrs = [];

    /// <summary>
    /// The allowed source ranges in CIDR notation. An explicit <c>"cidrs": null</c> is coalesced to an empty list,
    /// which is rejected on save and denies at evaluation rather than throwing.
    /// </summary>
    public IReadOnlyList<string> Cidrs
    {
        get => _cidrs;
        init => _cidrs = value ?? [];
    }

    public override AccessEvaluation Evaluate(AccessSignals signals)
    {
        // An empty allowlist or an unknown caller IP fails closed.
        if (Cidrs.Count == 0 || signals.IpAddress is null)
        {
            return AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
        }

        return Cidrs.Any(cidr => IPNetwork.TryParse(cidr, out var network) && network.Contains(signals.IpAddress))
            ? AccessEvaluation.Allow
            : AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
    }

    public override AccessRuleValidationResult Validate()
    {
        if (Cidrs.Count == 0)
        {
            return AccessRuleValidationResult.Invalid("ip_allowlist requires at least one CIDR.");
        }

        // Not FirstOrDefault: a null entry is itself invalid, so a null result would read as "every entry parsed".
        var invalidCidrs = Cidrs
            .Where(cidr => string.IsNullOrWhiteSpace(cidr) || !IPNetwork.TryParse(cidr, out _))
            .Take(1)
            .ToList();

        return invalidCidrs.Count > 0
            ? AccessRuleValidationResult.Invalid($"Invalid CIDR: '{invalidCidrs[0]}'.")
            : AccessRuleValidationResult.Valid;
    }
}
