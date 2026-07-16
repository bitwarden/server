using System.Net;
using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// Evaluates the access rule's flat list of <see cref="AccessCondition"/>s against the caller's signals. Each
/// condition yields an <see cref="AccessEvaluation"/>; the results combine with deny taking precedence over a
/// pending approval, which in turn takes precedence over allow. An empty list is vacuously satisfied (allow).
/// Unparseable inputs fail closed before they reach the engine.
/// </summary>
public sealed class AccessRuleEngine : IAccessRuleEngine
{
    public AccessEvaluation Evaluate(IReadOnlyList<AccessCondition> conditions, AccessSignals signals) =>
        AccessEvaluation.Combine(conditions.Select(condition => EvaluateCondition(condition, signals)));

    private static AccessEvaluation EvaluateCondition(AccessCondition condition, AccessSignals signals) => condition switch
    {
        HumanApprovalCondition => AccessEvaluation.RequiresApproval,
        IpAllowlistCondition ip => EvaluateIpAllowlist(ip, signals),
        // A condition kind the engine does not understand cannot be shown to be satisfied, so deny.
        _ => AccessEvaluation.Deny(DenyReason.UnsupportedCondition),
    };

    private static AccessEvaluation EvaluateIpAllowlist(IpAllowlistCondition condition, AccessSignals signals)
    {
        // An allowlist with no entries permits no address; combined with an unknown caller IP, both fail closed.
        if (condition.Cidrs.Count == 0 || signals.IpAddress is null)
        {
            return AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
        }

        foreach (var cidr in condition.Cidrs)
        {
            if (IPNetwork.TryParse(cidr, out var network) && network.Contains(signals.IpAddress))
            {
                return AccessEvaluation.Allow;
            }
        }

        return AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
    }
}
