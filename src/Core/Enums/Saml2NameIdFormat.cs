namespace Bit.Core.Enums;

public enum Saml2NameIdFormat : byte
{
    NotConfigured = 0,
    Unspecified = 1,
    EmailAddress = 2,
    X509SubjectName = 3,
    WindowsDomainQualifiedName = 4,
    KerberosPrincipalName = 5,
    EntityIdentifier = 6,
    Persistent = 7,
    Transient = 8,
}
