namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// An account recovery key re-wrapped with a member's V2 user key, to replace the V1 one. A null key unenrolls
/// the member instead, which is how an upgrade that cannot be completed is cleared.
/// </summary>
public class OrganizationUserAccountRecoveryKeyUpdate
{
    public Guid OrganizationUserId { get; init; }

    /// <summary>
    /// The key id of the user key the new <see cref="AccountRecoveryKey"/> wraps. The write only applies when this
    /// still matches the user row, so a rotation during the upgrade cannot be overwritten.
    /// </summary>
    public required string UserKeyId { get; init; }

    /// <summary>
    /// The V2 user key wrapped with the organization's public key, or null to unenroll the member from account
    /// recovery.
    /// </summary>
    public required string? AccountRecoveryKey { get; init; }
}
