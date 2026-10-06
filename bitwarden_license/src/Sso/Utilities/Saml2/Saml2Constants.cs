namespace Bit.Sso.Utilities.Saml2;

public static class Saml2ClaimTypes
{
    public const string Email = "urn:oid:0.9.2342.19200300.100.1.3";
    public const string GivenName = "urn:oid:2.5.4.42";
    public const string Surname = "urn:oid:2.5.4.4";
    public const string DisplayName = "urn:oid:2.16.840.1.113730.3.1.241";
    public const string CommonName = "urn:oid:2.5.4.3";
    public const string UserId = "urn:oid:0.9.2342.19200300.100.1.1";
}

public static class Saml2KeyTransportEncryptionAlgorithms
{
    public const string RsaOaepMgf1p = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";

    public const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

    public const string Rsa15 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";

    /// <summary>
    /// Key-transport algorithms advertised in Service Provider (SP) metadata.
    /// Order is important! rsa-oaep-mgf1p must come first.
    /// IdPs will generally choose the first advertised method found.
    /// rsa-oaep is more generic, and requires an IdP to also transmit a
    /// Digest Method. Without a Digest Method, rsa-oaep
    /// will throw on decryption.
    /// </summary>
    public static readonly string[] Accepted = [RsaOaepMgf1p, RsaOaep];
}

public static class Saml2NameIdFormats
{
    // Common
    public const string Unspecified = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";
    public const string Email = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";
    public const string Persistent = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent";
    public const string Transient = "urn:oasis:names:tc:SAML:2.0:nameid-format:transient";
    // Not-so-common
    public const string Upn = "http://schemas.xmlsoap.org/claims/UPN";
    public const string CommonName = "http://schemas.xmlsoap.org/claims/CommonName";
    public const string X509SubjectName = "urn:oasis:names:tc:SAML:1.1:nameid-format:X509SubjectName";
    public const string WindowsQualifiedDomainName = "urn:oasis:names:tc:SAML:1.1:nameid-format:WindowsDomainQualifiedName";
    public const string KerberosPrincipalName = "urn:oasis:names:tc:SAML:2.0:nameid-format:kerberos";
    public const string EntityIdentifier = "urn:oasis:names:tc:SAML:2.0:nameid-format:entity";
}

public static class Saml2PropertyKeys
{
    public const string ClaimFormat = "http://schemas.xmlsoap.org/ws/2005/05/identity/claimproperties/format";
}

public static class Saml2ResponseTypes
{
    public const string SuccessStatus = "urn:oasis:names:tc:SAML:2.0:status:Success";
    public const string ResponderStatus = "urn:oasis:names:tc:SAML:2.0:status:Responder";
    public const string AuthnFailedStatus = "urn:oasis:names:tc:SAML:2.0:status:AuthnFailed";
}
