namespace Bit.Pam.Enums;

/// <summary>
/// Who made an <see cref="Entities.AccessDecision"/>: the governing access rule, automatically, or a human approver.
/// </summary>
public enum AccessDeciderKind : byte
{
    Automatic = 0,

    Human = 1,
}
