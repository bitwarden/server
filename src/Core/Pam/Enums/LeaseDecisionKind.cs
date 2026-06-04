namespace Bit.Core.Pam.Enums;

/// <summary>
/// Who made a <see cref="Entities.LeaseDecision"/>: an automated policy evaluation or a human approver.
/// </summary>
public enum LeaseDecisionKind : byte
{
    Policy = 0,
    Human = 1,
}

/// <summary>
/// The verdict recorded on a <see cref="Entities.LeaseDecision"/>.
/// </summary>
public enum LeaseDecisionVerdict : byte
{
    Approve = 0,
    Deny = 1,
}
