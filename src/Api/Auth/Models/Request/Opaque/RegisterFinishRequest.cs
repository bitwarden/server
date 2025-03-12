namespace Bit.Api.Auth.Models.Request.Opaque;

public class RegisterFinishRequest
{
    public String ClientRegisterFinishResult { get; set; }
    public Guid SessionId { get; set; }
}

public class RotateableKeyset
{
    public String EncryptedUserKey { get; set; }
    public String EncryptedPublicKey { get; set; }
    public String EncryptedPrivateKey { get; set; }
}
