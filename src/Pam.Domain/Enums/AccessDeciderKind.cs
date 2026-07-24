namespace Bit.Pam.Enums;

/// <summary>
/// Who made a <see cref="Entities.AccessDecision"/>: an automatic condition evaluation or a human approver.
/// </summary>
public enum AccessDeciderKind : byte
{
    /// <summary>A condition on the governing access rule decided, with no human involved.</summary>
    Automatic = 0,

    /// <summary>A human approver decided.</summary>
    Human = 1,
}

/// <summary>
/// The verdict recorded on a <see cref="Entities.AccessDecision"/>.
/// </summary>
public enum AccessDecisionVerdict : byte
{
    /// <summary>Access was refused.</summary>
    Deny = 0,

    /// <summary>Access was granted.</summary>
    Approve = 1,
}
