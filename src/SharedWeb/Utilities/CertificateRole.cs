using System.ComponentModel.DataAnnotations;

namespace Bit.SharedWeb.Utilities;

/// <summary>
/// The role that a certificate has in a host.
/// </summary>
public enum CertificateRole
{
    /// <summary>
    /// The role that signs the Identity host tokens.
    /// </summary>
    [Display(Name = "Identity signing")]
    IdentitySigning = 0,
    /// <summary>
    /// The role that signs the SSO host OIDC tokens, the SAML requests, and the SAML metadata.
    /// </summary>
    [Display(Name = "SSO signing")]
    SsoSigning = 1,
    /// <summary>
    /// The role that decrypts the SAML assertions of the SSO host.
    /// </summary>
    [Display(Name = "SSO encryption")]
    SsoEncryption = 2,
}
