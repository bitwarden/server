namespace Bit.Services.Pam.Engine;

/// <summary>
/// The caller-facing wording for a denied <see cref="AccessEvaluation"/>. One home for it because the two gates that
/// evaluate a rule's conditions have to say the same thing: submit refuses a request the conditions do not admit, and
/// activation refuses to mint a lease when they no longer admit it. A requester who is told "not from your current
/// network" at one gate and something else at the other cannot tell that the same rule refused them twice.
/// </summary>
/// <remarks>
/// Deliberately vague about which condition refused and why: the message reaches a member who cannot see the rule, and
/// naming the failing CIDR or window would let them probe the configuration. The precise <see cref="DenyReason"/> goes
/// to the audit trail instead, where an admin can read it.
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
