namespace Bit.Core.Vault.Models.Data;

public class CipherSSHKeyData : CipherData
{
    public CipherSSHKeyData() { }

    public string PrivateKey { get; set; }
    public string PublicKey { get; set; }
    public string KeyFingerprint { get; set; }
}
