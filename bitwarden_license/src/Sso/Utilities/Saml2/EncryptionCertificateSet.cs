using System.Security.Cryptography.X509Certificates;

namespace Bit.Sso.Utilities.Saml2;

/// <summary>
/// The certificates that the SSO encryption role uses, after the Identity fallback. <see cref="Active"/> decrypts
/// SAML assertions, and the SAML metadata publishes it. The <see cref="Staged"/> and <see cref="Retired"/>
/// certificates decrypt SAML assertions, and the SAML metadata does not publish them. <see cref="IsFallback"/> is
/// true when the role uses the Identity certificates because it has no active source of its own.
/// </summary>
public sealed record EncryptionCertificateSet(
    X509Certificate2? Active,
    IReadOnlyList<X509Certificate2> Staged,
    IReadOnlyList<X509Certificate2> Retired,
    bool IsFallback);
