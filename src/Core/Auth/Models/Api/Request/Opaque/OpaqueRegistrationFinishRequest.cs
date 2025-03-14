using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.Opaque;

public class OpaqueRegistrationFinishRequest
{
    [Required]
    public string RegistrationUpload { get; set; }
    [Required]
    public Guid SessionId { get; set; }

    public RotateableOpaqueKeyset KeySet { get; set; }
}

public class RotateableOpaqueKeyset
{
    [Required]
    public string EncryptedUserKey { get; set; }
    [Required]
    public string EncryptedPublicKey { get; set; }
    [Required]
    public string EncryptedPrivateKey { get; set; }
}
