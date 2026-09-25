namespace Bit.Core.KeyManagement.UserKey.Models.Data;

/// <summary>
/// All data not included in a sync that the client needs to rewrap userKey encrypted data for user key rotation. The server performs all
/// filtering and public-key joins so the client can fetch everything in a single request.
/// </summary>
public class KeyRotationData
{
    /// <summary>
    /// Organizations the user is reset-password-enrolled in. The org public key is used to re-wrap the
    /// org-recovery key.
    /// </summary>
    public IEnumerable<OrganizationPasswordResetKeyData> OrganizationPasswordResetKeyData { get; set; } = [];

    /// <summary>
    /// Emergency access memberships granted by this user.
    /// </summary>
    public IEnumerable<EmergencyAccessKeyData> EmergencyAccessKeyData { get; set; } = [];

    /// <summary>
    /// The user's trusted devices.
    /// </summary>
    public IEnumerable<TrustedDeviceKeyData> TrustedDeviceKeyData { get; set; } = [];

    /// <summary>
    /// The user's PRF-enabled passkeys.
    /// </summary>
    public IEnumerable<PasskeyKeyData> PasskeyKeyData { get; set; } = [];
}
