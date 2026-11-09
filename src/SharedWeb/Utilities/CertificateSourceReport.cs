using System.Security.Cryptography.X509Certificates;

namespace Bit.SharedWeb.Utilities;

/// <summary>
/// The certificates that one role loaded at startup. The host logs this report at startup. <see cref="Active"/> is
/// null when the role has no certificate. <see cref="Retired"/> holds the validation or decryption certificates.
/// <see cref="IsFallback"/> is true when the role uses the Identity certificates because it has no active source of
/// its own.
/// </summary>
public sealed record CertificateSourceReport(
    string Role,
    X509Certificate2? Active,
    IReadOnlyList<X509Certificate2> Staged,
    IReadOnlyList<X509Certificate2> Retired,
    bool IsFallback)
{
    /// <summary>
    /// The role that signs the Identity host tokens.
    /// </summary>
    public const string IdentitySigning = "Identity signing";
    /// <summary>
    /// The role that signs the SSO host OIDC tokens, the SAML requests, and the SAML metadata.
    /// </summary>
    public const string SsoSigning = "SSO signing";
    /// <summary>
    /// The role that decrypts the SAML assertions of the SSO host.
    /// </summary>
    public const string SsoEncryption = "SSO encryption";
}
