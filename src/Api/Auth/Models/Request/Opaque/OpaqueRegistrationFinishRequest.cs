using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Opaque;

public class OpaqueRegistrationFinishRequest
{
    [Required]
    public String RegistrationUpload { get; set; }
    [Required]
    public Guid SessionId { get; set; }

    public RotateableKeyset KeySet { get; set; }
}

public class RotateableKeyset
{
    [Required]
    public String EncryptedUserKey { get; set; }
    [Required]
    public String EncryptedPublicKey { get; set; }
    [Required]
    public String EncryptedPrivateKey { get; set; }
}
