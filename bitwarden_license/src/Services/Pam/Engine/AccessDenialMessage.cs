namespace Bit.Services.Pam.Engine;

/// <summary>
/// Caller-facing wording for a denied <see cref="AccessEvaluation"/>. Never names the failing value (a CIDR or
/// window), so a member cannot probe the rule's configuration.
/// </summary>
public static class AccessDenialMessage
{
    public static string For(AccessEvaluation evaluation) => evaluation.Reason switch
    {
        DenyReason.NotWithinIpRange => "Access to this item is not permitted from your current network.",
        DenyReason.NotWithinTimeWindow => "Access to this item is not permitted at this time.",
        _ => "Access to this item is not permitted right now.",
    };
}
