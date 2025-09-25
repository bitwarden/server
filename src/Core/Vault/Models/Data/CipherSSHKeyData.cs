// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Vault.Models.Data;

public class CipherSSHKeyData : CipherData
{
    public CipherSSHKeyData() { }

    public string PrivateKey { get; set; }
    public string PublicKey { get; set; }
    public string KeyFingerprint { get; set; }

    // New fields to preserve original encrypted key and optional passphrase
    public string OriginalPrivateKey { get; set; }
    // Booleans are typically transported as encrypted strings ("true"/"false") in Bitwarden models
    public string IsEncrypted { get; set; }
    public string SshKeyPassphrase { get; set; }
}
