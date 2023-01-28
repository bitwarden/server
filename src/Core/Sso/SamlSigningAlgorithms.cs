namespace Bit.Core.Sso;

public static class SamlSigningAlgorithms
{
    public const string Default = Sha256;
    public const string Sha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    public const string Sha384 = "http://www.w3.org/2000/09/xmldsig#rsa-sha384";
    public const string Sha512 = "http://www.w3.org/2000/09/xmldsig#rsa-sha512";

    public static IEnumerable<string> GetEnumerable()
    {
        yield return Sha256;
        yield return Sha384;
        yield return Sha512;
    }
}
