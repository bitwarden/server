using System.Security.Cryptography.X509Certificates;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Billing.Licenses.Services;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Services;

/// <inheritdoc />
/// <remarks>Caches the loaded signing certificate across runs.</remarks>
public sealed class SeederLicenseSigner : ISeederLicenseSigner, IDisposable
{
    private readonly ILicenseClaimsFactory<User> _userLicenseClaimsFactory;
    private readonly Lazy<CertificateLoad> _certificate;

    public SeederLicenseSigner(
        IGlobalSettings globalSettings,
        ILicenseClaimsFactory<User> userLicenseClaimsFactory,
        ILogger<SeederLicenseSigner> logger)
        : this(globalSettings, userLicenseClaimsFactory, logger, LicensingCertificateThumbprints.All)
    {
    }

    internal SeederLicenseSigner(
        IGlobalSettings globalSettings,
        ILicenseClaimsFactory<User> userLicenseClaimsFactory,
        ILogger<SeederLicenseSigner> logger,
        IReadOnlySet<string> allowedThumbprints)
    {
        _userLicenseClaimsFactory = userLicenseClaimsFactory;
        _certificate = new Lazy<CertificateLoad>(() => LoadCertificate(globalSettings, logger, allowedThumbprints));
    }

    public async Task<LicenseSigningResult> CreateUserTokenAsync(User user)
    {
        var (certificate, warning) = _certificate.Value;
        if (certificate is null)
        {
            return LicenseSigningResult.Skipped(warning ?? "No signing certificate is available.");
        }

        var licenseContext = new LicenseContext { SubscriptionInfo = new SubscriptionInfo() };
        var claims = await _userLicenseClaimsFactory.GenerateClaims(user, licenseContext);
        var audience = $"user:{user.Id}";

        return LicenseSigningResult.Signed(LicenseTokenGenerator.Generate(certificate, claims, audience));
    }

    /// <summary>
    /// Loads the signing certificate. Returns a null certificate plus the reason when it is not
    /// usable for signing. Never throws. Invoked once via <see cref="Lazy{T}"/>.
    /// </summary>
    private static CertificateLoad LoadCertificate(
        IGlobalSettings globalSettings, ILogger logger, IReadOnlySet<string> allowedThumbprints)
    {
        if (!CoreHelpers.SettingHasValue(globalSettings.LicenseCertificatePath) ||
            !CoreHelpers.SettingHasValue(globalSettings.LicenseCertificatePassword))
        {
            return Unusable(logger,
                "No signing certificate configured (licenseCertificatePath/licenseCertificatePassword). " +
                "Skipping premium license generation.");
        }

        if (!File.Exists(globalSettings.LicenseCertificatePath))
        {
            return Unusable(logger,
                "Configured licensing certificate file was not found. Skipping premium license generation.");
        }

        X509Certificate2 certificate;
        try
        {
            certificate = CoreHelpers.GetCertificate(
                globalSettings.LicenseCertificatePath, globalSettings.LicenseCertificatePassword);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to load the configured licensing certificate. Skipping premium license generation.");
            return new CertificateLoad(null,
                "Failed to load the configured licensing certificate. Skipping premium license generation.");
        }

        using (var rsa = certificate.GetRSAPrivateKey())
        {
            if (rsa is null)
            {
                certificate.Dispose();
                return Unusable(logger,
                    "Configured licensing certificate has no RSA private key and cannot sign licenses. " +
                    "Skipping premium license generation.");
            }
        }

        if (!allowedThumbprints.Contains(CoreHelpers.CleanCertificateThumbprint(certificate.Thumbprint)))
        {
            certificate.Dispose();
            return Unusable(logger,
                "Configured licensing certificate is not a trusted Bitwarden licensing certificate; " +
                "a self-hosted instance would reject the resulting license. Skipping premium license generation.");
        }

        var thumbprintPrefix = certificate.Thumbprint is { Length: >= 8 } tp ? tp[..8] : certificate.Thumbprint;
        logger.LogInformation("Using licensing certificate with thumbprint {ThumbprintPrefix}… for premium license signing.",
            thumbprintPrefix);

        return new CertificateLoad(certificate, null);
    }

    private static CertificateLoad Unusable(ILogger logger, string warning)
    {
        logger.LogWarning("{Warning}", warning);
        return new CertificateLoad(null, warning);
    }

    public void Dispose()
    {
        if (_certificate.IsValueCreated)
        {
            _certificate.Value.Certificate?.Dispose();
        }
    }

    private readonly record struct CertificateLoad(X509Certificate2? Certificate, string? Warning);
}
