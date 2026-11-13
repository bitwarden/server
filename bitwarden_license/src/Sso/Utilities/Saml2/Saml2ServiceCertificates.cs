using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Configuration;

namespace Bit.Sso.Utilities.Saml2;

/// <summary>
/// Gets the SSO encryption certificates, and builds the SAML service provider certificates of the SSO host
/// from the signing and encryption certificates.
/// </summary>
public static class Saml2ServiceCertificates
{
    /// <summary>
    /// Gets the SSO encryption certificates. An SSO encryption active source uses only the SSO encryption slots.
    /// With no SSO encryption active source, a self-hosted or Development host uses the Identity active and staged
    /// certificates from <paramref name="identity"/> and the SSO encryption slots. A cloud host that is not a
    /// Development host must have an SSO encryption active source.
    /// </summary>
    /// <exception cref="InvalidOperationException">A cloud host has no SSO encryption active source, or a configured
    /// certificate did not load, or a configured certificate does not have an RSA key.</exception>
    public static EncryptionCertificateSet GetEncryptionCertificates(IWebHostEnvironment env,
        GlobalSettings globalSettings, IdentityCertificates identity)
    {
        const string encryptionPath = "globalSettings:ssoServer:encryption";
        var encryption = globalSettings.SsoServer.Encryption;
        var sso = SsoCertificateSlots.Load(env, globalSettings, encryption, encryption.FutureCertificate,
            encryption.DecryptionCertificates, encryptionPath, $"{encryptionPath}:decryptionCertificates",
            "SSO encryption");
        if (!sso.IsFallback)
        {
            return new EncryptionCertificateSet(sso.Active, sso.Staged, sso.Retired, IsFallback: false);
        }

        return new EncryptionCertificateSet(identity.Active, [.. identity.Staged, .. sso.Staged], sso.Retired,
            IsFallback: true);
    }

    /// <summary>
    /// Builds the SAML service certificates. Only the active signing certificate signs. A staged signing certificate
    /// is published as a future signing certificate, only when an active signing certificate exists. Only the active
    /// encryption certificate is published for encryption. The staged and decryption encryption certificates decrypt,
    /// and are not published.
    /// </summary>
    /// <exception cref="InvalidOperationException">The SAML library rejects a certificate, for example a certificate
    /// with no private key, or an encryption certificate with no RSA key. The message names the role and the
    /// certificate thumbprint, and the inner exception is the library error.</exception>
    public static ServiceCertificateCollection Build(SigningCertificateSet signing,
        EncryptionCertificateSet encryption)
    {
        var entries = new ServiceCertificateCollection();
        if (signing.Active != null && signing.Active.Thumbprint == encryption.Active?.Thumbprint)
        {
            Add(entries, signing.Active, CertificateUse.Both, CertificateStatus.Current);
        }
        else
        {
            Add(entries, signing.Active, CertificateUse.Signing, CertificateStatus.Current);
            Add(entries, encryption.Active, CertificateUse.Encryption, CertificateStatus.Current);
        }

        var stagedSigning = signing.Active == null ? [] : UniqueExcept(signing.Staged, signing.Active);
        foreach (var certificate in stagedSigning)
        {
            Add(entries, certificate, CertificateUse.Signing, CertificateStatus.Future);
        }

        foreach (var certificate in UniqueExcept(encryption.Staged.Concat(encryption.Decryption), encryption.Active))
        {
            Add(entries, certificate, CertificateUse.Encryption, CertificateStatus.Current,
                MetadataPublishOverrideType.DoNotPublish);
        }
        return entries;
    }

    private static IEnumerable<X509Certificate2> UniqueExcept(IEnumerable<X509Certificate2> certificates,
        X509Certificate2? excluded) =>
        certificates
            .Where(certificate => certificate.Thumbprint != excluded?.Thumbprint)
            .DistinctBy(certificate => certificate.Thumbprint);

    private static void Add(ServiceCertificateCollection entries, X509Certificate2? certificate, CertificateUse use,
        CertificateStatus status, MetadataPublishOverrideType publishOverride = MetadataPublishOverrideType.None)
    {
        if (certificate == null)
        {
            return;
        }

        try
        {
            entries.Add(new ServiceCertificate
            {
                Certificate = certificate,
                Use = use,
                Status = status,
                MetadataPublishOverride = publishOverride,
            });
        }
        catch (Exception ex)
        {
            var role = use switch
            {
                CertificateUse.Signing => "SSO signing",
                CertificateUse.Encryption => "SSO encryption",
                _ => "SSO signing and SSO encryption",
            };
            throw new InvalidOperationException(
                $"The SAML library rejected the {role} certificate with thumbprint {certificate.Thumbprint}. " +
                "Check that the certificate file or store entry includes its private key, and that an encryption " +
                "certificate has an RSA key.", ex);
        }
    }
}
