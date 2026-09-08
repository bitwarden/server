namespace Bit.Sso.Utilities;

public static class SamlEncryptionAlgorithms
{
    public const string RsaOaepMgf1p = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";

    public const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

    // Key-transport algorithms advertised in Service Provider (SP) metadata.
    // Order is important! rsa-oaep-mgf1p must come first.
    // IdPs will generally choose the first advertised method found.
    // rsa-oaep is more generic, and requires an IdP to also transmit a
    // <ds:DigestMethod> Digest Method. Without a Digest Method, rsa-oaep
    // will throw on decryption.
    public static readonly string[] Accepted = [RsaOaepMgf1p, RsaOaep];
}
