// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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

        // Map new optional properties if present
        OriginalPrivateKey = data.OriginalPrivateKey;
        IsEncrypted = data.IsEncrypted;
        SshKeyPassphrase = data.SshKeyPassphrase;
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

    // Preserve original encrypted PEM verbatim
    [EncryptedString]
    [EncryptedStringLength(5000)]
    public string OriginalPrivateKey { get; set; }

    // Transported as encrypted string ("true"/"false")
    [EncryptedString]
    [EncryptedStringLength(10)]
    public string IsEncrypted { get; set; }

    // Optional stored passphrase (encrypted)
    [EncryptedString]
    [EncryptedStringLength(5000)]
    public string SshKeyPassphrase { get; set; }
}
