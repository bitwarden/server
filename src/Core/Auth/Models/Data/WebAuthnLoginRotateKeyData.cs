// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Data;

public class WebAuthnLoginRotateKeyData
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }

}
