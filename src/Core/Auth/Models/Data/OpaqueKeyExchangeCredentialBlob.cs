namespace Bit.Core.Auth.Models.Data;
public class OpaqueKeyExchangeCredentialBlob
{
    public byte[] PasswordFile { get; set; }
    public byte[] ServerSetup { get; set; }
}
