using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherSSHKeyModel
{
    public CipherSSHKeyModel() { }

    public CipherSSHKeyModel(CipherSSHKeyData data)
    {
        PrivateKey = data.PrivateKey;
        PublicKey = data.PublicKey;
        KeyFingerprint = data.KeyFingerprint;
    }

    [EncryptedString]
    [EncryptedStringLength(5000)]
    public string PrivateKey { get; set; }

    [EncryptedString]
    [EncryptedStringLength(5000)]
    public string PublicKey { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string KeyFingerprint { get; set; }
}
