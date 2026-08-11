namespace Bit.Pam.Enums;

/// <summary>
/// Who made a <see cref="Entities.AccessDecision"/>: an automatic condition evaluation or a human approver.
/// </summary>
public enum AccessDeciderKind : byte
{
    /// <summary>A condition on the governing access rule decided automatically, with no human approver.</summary>
    Automatic = 0,

    /// <summary>A human approver made the decision.</summary>
    Human = 1,
}

/// <summary>
/// The verdict recorded on a <see cref="Entities.AccessDecision"/>.
/// </summary>
public enum AccessDecisionVerdict : byte
{
    /// <summary>Access was refused; no lease is produced.</summary>
    Deny = 0,

    /// <summary>Access was granted; an approved request can then be activated into a lease.</summary>
    Approve = 1,
}
