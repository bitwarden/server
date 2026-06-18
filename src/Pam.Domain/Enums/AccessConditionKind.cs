namespace Bit.Pam.Enums;

/// <summary>
/// The kind of access condition that produced an automatic <see cref="Entities.AccessDecision"/>. Mirrors the
/// <c>kind</c> discriminator on <see cref="Models.Conditions.AccessCondition"/>, which remains the JSON wire format
/// for a rule's conditions; this enum is the persisted, type-safe form recorded against the decision.
/// </summary>
public enum AccessConditionKind : byte
{
    HumanApproval = 0,
    IpAllowlist = 1,
    TimeOfDay = 2,
}
