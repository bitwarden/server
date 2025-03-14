namespace Bit.Core.Auth.Models.Data;
public class OpaqueKeyExchangeCredentialBlob
{
    public byte[] ClientSetup { get; set; }
    public byte[] ServerSetup { get; set; }
}
