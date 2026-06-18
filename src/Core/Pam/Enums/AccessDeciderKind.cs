namespace Bit.Pam.Enums;

/// <summary>
/// Who made a <see cref="Entities.AccessDecision"/>: an automatic condition evaluation or a human approver.
/// </summary>
public enum AccessDeciderKind : byte
{
    Automatic = 0,
    Human = 1,
}

/// <summary>
/// The verdict recorded on a <see cref="Entities.AccessDecision"/>.
/// </summary>
public enum AccessDecisionVerdict : byte
{
    Deny = 0,
    Approve = 1,
}
