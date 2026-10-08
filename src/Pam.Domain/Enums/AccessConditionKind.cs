namespace Bit.Pam.Enums;

/// <summary>
/// The kind of access condition behind an automatic <see cref="Entities.AccessDecision"/>. Mirrors the <c>kind</c>
/// discriminator of a rule's JSON <c>AccessCondition</c>, persisted against the decision.
/// </summary>
public enum AccessConditionKind : byte
{
    HumanApproval = 0,

    IpAllowlist = 1,
}
