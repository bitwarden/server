using System.Net;

namespace Bit.Core.PrivilegedAccessManagement.Engine.Conditions;

public sealed class IpRangeCondition : IAccessCondition
{
    public AccessDecision Evaluate(AccessRuleEngineContext context)
    {
        var requiredCidr = context.Rule.RequiredCidr;
        if (requiredCidr.Count == 0)
        {
            return AccessDecision.Allow;
        }

        foreach (var cidr in requiredCidr)
        {
            if (IPNetwork.TryParse(cidr, out var network) && network.Contains(context.Signals.IpAddress))
            {
                return AccessDecision.Allow;
            }
        }

        return AccessDecision.Deny(DenyReason.NotWithinIpRange);
    }
}
