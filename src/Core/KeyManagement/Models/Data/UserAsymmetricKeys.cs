#nullable enable
namespace Bit.Core.KeyManagement.Models.Data;

public class UserAsymmetricKeys
{
    public Guid UserId { get; set; }
    public required string PublicKey { get; set; }
    public required string UserKeyEncryptedPrivateKey { get; set; }
}
