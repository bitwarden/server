namespace Bit.Services.Pam.Enums;

/// <summary>
/// The approval path a lease request will take: <see cref="Automatic"/> (pick a duration) or <see cref="Human"/>
/// (pick a window and justify).
/// </summary>
public enum AccessApprovalMode : byte
{
    Automatic = 0,
    Human = 1,
}
