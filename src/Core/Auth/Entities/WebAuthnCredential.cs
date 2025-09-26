// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

public class WebAuthnCredential : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(256)]
    public string PublicKey { get; set; }
    [MaxLength(256)]
    public string CredentialId { get; set; }
    public int Counter { get; set; }
    [MaxLength(20)]
    public string Type { get; set; }
    public Guid AaGuid { get; set; }

    /// <summary>
    /// User key encrypted with this WebAuthn credential's public key (EncryptedPublicKey field).
    /// </summary>
    [MaxLength(2000)]
    public string EncryptedUserKey { get; set; }

    /// <summary>
    /// Private key encrypted with an external key for secure storage.
    /// </summary>
    [MaxLength(2000)]
    public string EncryptedPrivateKey { get; set; }

    /// <summary>
    /// Public key encrypted with the user key for key rotation.
    /// </summary>
    [MaxLength(2000)]
    public string EncryptedPublicKey { get; set; }

    /// <summary>
    /// Indicates whether this credential supports PRF (Pseudo-Random Function) extension.
    /// </summary>
    public bool SupportsPrf { get; set; }

    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public WebAuthnPrfStatus GetPrfStatus()
    {
        if (!SupportsPrf)
        {
            return WebAuthnPrfStatus.Unsupported;
        }

        if (EncryptedUserKey != null && EncryptedPrivateKey != null && EncryptedPublicKey != null)
        {
            return WebAuthnPrfStatus.Enabled;
        }

        return WebAuthnPrfStatus.Supported;
    }
}
