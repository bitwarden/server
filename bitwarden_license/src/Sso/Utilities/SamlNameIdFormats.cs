namespace Bit.Sso.Utilities;

public static class SamlNameIdFormats
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
