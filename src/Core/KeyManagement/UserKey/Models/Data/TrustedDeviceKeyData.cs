namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class TrustedDeviceKeyData
{
    public Guid Id { get; set; }
    public required string EncryptedPublicKey { get; set; }
    public required string EncryptedUserKey { get; set; }
}
