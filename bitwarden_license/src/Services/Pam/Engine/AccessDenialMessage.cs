namespace Bit.Services.Pam.Engine;

/// <summary>
/// The caller-facing wording for a denied <see cref="AccessEvaluation"/>, shared so submit and activation refuse
/// with the same message for the same rule.
/// </summary>
/// <remarks>
/// Deliberately vague about which condition refused: naming the failing CIDR or window would let a member probe
/// the configuration. The precise <see cref="DenyReason"/> goes to the audit trail instead.
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
