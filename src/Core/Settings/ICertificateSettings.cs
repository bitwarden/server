namespace Bit.Core.Settings;

/// <summary>
/// Identifies the source of one certificate. A cloud host loads the certificate from blob storage
/// or from the certificate store. A self-hosted host loads the certificate from a file.
/// IDENTITY_SSO_CERTIFICATE_ROTATION.md, in this folder, describes the certificate roles, the settings, and rotation.
/// </summary>
public interface ICertificateSettings
{
    /// <summary>
    /// The path of the certificate file. A self-hosted host uses this path. A cloud host does not.
    /// </summary>
    string? CertificateLocation { get; set; }
    /// <summary>
    /// The thumbprint of the certificate in the certificate store. A cloud host and a self-hosted host may use this value.
    /// </summary>
    string? CertificateThumbprint { get; set; }
    /// <summary>
    /// The password of the certificate file or the certificate blob. A cloud host and a self-hosted host use this value.
    /// </summary>
    string? CertificatePassword { get; set; }
    /// <summary>
    /// The name of the certificate blob in the "certificates" blob container. A cloud host uses this name. A self-hosted host does not.
    /// </summary>
    string? BlobName { get; set; }
}
