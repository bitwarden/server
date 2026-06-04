namespace Bit.Core.Pam.Enums;

/// <summary>
/// The approval path a lease request will take, surfaced by the pre-check so the client can present the right
/// workflow: <see cref="Automatic"/> (pick a duration) or <see cref="Human"/> (pick a window + justify).
/// </summary>
public enum AccessApprovalOutcome
{
    Automatic,
    Human,
}
