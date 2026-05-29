namespace Bit.Core.PrivilegedAccessManagement.Engine;

public enum DecisionKind
{
    Allow,
    RequiresApproval,
    Deny,
}

public enum DenyReason
{
    None = 0,
    NotWithinIpRange,
    SingletonHeld,
    NotWithinTimeWindow,
}

public sealed record AccessDecision
{
    public required DecisionKind Kind { get; init; }
    public DenyReason Reason { get; init; } = DenyReason.None;

    public static AccessDecision Allow { get; } = new() { Kind = DecisionKind.Allow };
    public static AccessDecision RequiresApproval { get; } = new() { Kind = DecisionKind.RequiresApproval };

    public static AccessDecision Deny(DenyReason reason) => new()
    {
        Kind = DecisionKind.Deny,
        Reason = reason
    };

    public static AccessDecision Combine(IEnumerable<AccessDecision> decisions)
    {
        var requiresApproval = false;
        foreach (var decision in decisions)
        {
            switch (decision.Kind)
            {
                case DecisionKind.Deny:
                    return decision;
                case DecisionKind.RequiresApproval:
                    requiresApproval = true;
                    break;
            }
        }

        return requiresApproval ? RequiresApproval : Allow;
    }
}
