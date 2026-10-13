namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// A membership whose account recovery key is still wrapped with the member's V1 user key, with the data an
/// organization admin needs to re-wrap it with the V2 user key.
/// </summary>
/// <remarks>
/// Only rows that have all three of a V2 upgrade token, an account recovery key, and a user key id are returned.
/// Without the user key id the server cannot verify which key the admin re-wrapped, so the upgrade is not offered.
/// </remarks>
public class OrganizationUserV2UpgradeDetails
{
    public Guid OrganizationUserId { get; set; }

    /// <summary>
    /// The key id of the member's current user key, read from the user row. The admin returns this value with the
    /// upgrade so that the server can reject a re-wrap of a key that has been rotated again since.
    /// </summary>
    public string UserKeyId { get; set; } = null!;

    /// <summary>
    /// The V1 user key wrapped with the organization's public key. The admin unwraps it with the organization's
    /// private key.
    /// </summary>
    public string AccountRecoveryKey { get; set; } = null!;

    /// <summary>
    /// The V2 upgrade token as stored, that is, JSON for <see cref="V2UpgradeTokenData"/>. Use
    /// <see cref="V2UpgradeTokenData.FromJson"/> to read it.
    /// </summary>
    public string V2UpgradeToken { get; set; } = null!;
}
