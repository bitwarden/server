namespace Bit.Pam.Enums;

/// <summary>
/// The kind of access condition that produced an automatic <see cref="Entities.AccessDecision"/>. Mirrors the
/// <c>kind</c> discriminator on <see cref="Models.Conditions.AccessCondition"/>, which remains the JSON wire format
/// for a rule's conditions; this enum is the persisted, type-safe form recorded against the decision.
/// </summary>
public enum AccessConditionKind : byte
{
    /// <summary>Requires a human approver; matching requests are routed for manual approval rather than auto-decided.</summary>
    HumanApproval = 0,

    /// <summary>Matches when the requester's IP is on a configured allowlist.</summary>
    IpAllowlist = 1,

    /// <summary>Matches when the request falls within a configured time-of-day window.</summary>
    TimeOfDay = 2,
}
