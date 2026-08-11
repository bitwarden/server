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
    private readonly IReadOnlyList<string> _cidrs = [];

    /// <summary>
    /// The allowed source ranges in CIDR notation (e.g. <c>"10.0.0.0/8"</c>). The condition allows when the caller's
    /// IP is in any one of them. At least one required, and each must parse; an empty list denies.
    /// </summary>
    /// <remarks>
    /// Never null. A <c>"cidrs": null</c> in the document deserializes as a null value, which would otherwise
    /// replace the empty default and make both members below throw on <c>Count</c> — an unhandled exception in
    /// place of the loud rejection at write time and the fail-closed deny at evaluation time. Coalescing here
    /// makes an explicit null behave exactly like an omitted or empty list.
    /// </remarks>
    public IReadOnlyList<string> Cidrs
    {
        get => _cidrs;
        init => _cidrs = value ?? [];
    }

    public override AccessEvaluation Evaluate(AccessSignals signals)
    {
        // An allowlist with no entries permits no address; combined with an unknown caller IP, both fail closed.
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

        // Take(1) preserves the short-circuit on the first bad entry without materialising an unbounded list.
        // FirstOrDefault is unsuitable here: a null entry is itself invalid, so a null result could not be
        // distinguished from "every entry parsed".
        var invalidCidrs = Cidrs
            .Where(cidr => string.IsNullOrWhiteSpace(cidr) || !IPNetwork.TryParse(cidr, out _))
            .Take(1)
            .ToList();

        return invalidCidrs.Count > 0
            ? AccessRuleValidationResult.Invalid($"Invalid CIDR: '{invalidCidrs[0]}'.")
            : AccessRuleValidationResult.Valid;
    }
}
