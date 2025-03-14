using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Opaque;

public class OpaqueRegistrationFinishRequest
{
    [Required]
    public string RegistrationUpload { get; set; }
    [Required]
    public Guid SessionId { get; set; }

    public RotateableKeyset KeySet { get; set; }
}

public class RotateableKeyset
{
    [Required]
    public string EncryptedUserKey { get; set; }
    [Required]
    public string EncryptedPublicKey { get; set; }
    [Required]
    public string EncryptedPrivateKey { get; set; }
}
