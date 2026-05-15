using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class Device : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(50)]
    public string Name { get; set; } = null!;
    public Enums.DeviceType Type { get; set; }
    [MaxLength(50)]
    public string Identifier { get; set; } = null!;
    [MaxLength(255)]
    public string? PushToken { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Intended to be the users symmetric key that is encrypted in some form, the current way to encrypt this is with
    /// the devices public key.
    /// </summary>
    public string? EncryptedUserKey { get; set; }

    /// <summary>
    /// Intended to be the public key that was generated for a device upon trust and encrypted. Currenly encrypted using
    /// a users symmetric key so that when trusted and unlocked a user can decrypt the public key for all their devices.
    /// This enabled a user to rotate the keys for all of their devices.
    /// </summary>
    public string? EncryptedPublicKey { get; set; }

    /// <summary>
    /// Intended to be the private key that was generated for a device upon trust and encrypted. Currenly encrypted with
    /// the devices key, that upon successful login a user can decrypt this value and therefor decrypt their vault.
    /// </summary>
    public string? EncryptedPrivateKey { get; set; }

    /// <summary>
    /// Whether the device is active for the user.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// The last time this device was logged in on or had a token refresh. Null if the device has not
    /// authenticated since activity tracking was introduced.
    /// </summary>
    public DateTime? LastActivityDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// The version of the client software the device was last seen running. Populated from the
    /// Bitwarden-Client-Version header on device creation and on every successful login / token refresh.
    /// Null if the device has not authenticated since client version tracking was introduced or if
    /// the header was absent.
    /// Sized to 43 chars — the upper bound of <see cref="Version.ToString()"/> for any input
    /// accepted by <see cref="Version.TryParse(string?, out Version?)"/>: four
    /// <see cref="int"/> components (max <see cref="int.MaxValue"/> = 10 digits) joined by 3 dots.
    /// Real Bitwarden CalVer <c>YYYY.M.B</c> is ~9 chars; the extra headroom prevents truncation
    /// errors from malformed/hostile headers that still parse as a <see cref="Version"/>.
    /// </summary>
    [MaxLength(43)]
    public string? ClientVersion { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
