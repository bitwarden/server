namespace Bit.Pam.Enums;

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
