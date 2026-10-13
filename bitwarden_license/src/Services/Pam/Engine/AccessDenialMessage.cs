namespace Bit.Services.Pam.Engine;

/// <summary>
/// The caller-facing wording for a denied <see cref="AccessEvaluation"/>, shared by submit and activation.
/// </summary>
/// <remarks>
/// Never names the failing value (a CIDR or window), so a member can't probe the rule's configuration.
/// </remarks>
public static class AccessDenialMessage
{
    public static string For(AccessEvaluation evaluation) => evaluation.Reason switch
    {
        DenyReason.NotWithinIpRange => "Access to this item is not permitted from your current network.",
        DenyReason.NotWithinTimeWindow => "Access to this item is not permitted at this time.",
        _ => "Access to this item is not permitted right now.",
    };
}
