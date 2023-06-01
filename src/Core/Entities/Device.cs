using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class Device : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    public Enums.DeviceType Type { get; set; }
    [MaxLength(50)]
    public string Identifier { get; set; }
    [MaxLength(255)]
    public string PushToken { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Intended to be the users symmetric key that is encrypted in some form, the current way to encrypt this is with
    /// the devices public key.
    /// </summary>
    public string EncryptedUserKey { get; set; }

    /// <summary>
    /// Intended to be the public key that was generated for a device upon trust and encrypted. Currenly encrypted using
    /// a users symmetric key so that when trusted and unlocked a user can decrypt the public key for all their devices.
    /// This enabled a user to rotate the keys for all of their devices.
    /// </summary>
    public string EncryptedPublicKey { get; set; }

    /// <summary>
    /// Intended to be the private key that was generated for a device upon trust and encrypted. Currenly encrypted with
    /// the devices key, that upon successful login a user can decrypt this value and therefor decrypt their vault.
    /// </summary>
    public string EncryptedPrivateKey { get; set; }


    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
