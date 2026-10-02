using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Billing.Licenses.Services;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Seeder.Services;

public sealed class SeederLicenseSigner : ISeederLicenseSigner, IDisposable
{
    private readonly ILicenseClaimsFactory<User> _userLicenseClaimsFactory;
    private readonly Lazy<CertificateLoad> _certificate;

    // Only the development licensing thumbprint is trusted. The production thumbprint from LicensingService
    // (src/Core/Billing/Services/Implementations/LicensingService.cs) is deliberately excluded here.
    private static readonly IReadOnlySet<string> _trustedThumbprints =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CoreHelpers.CleanCertificateThumbprint("207E64A231E8AA32AAF68A61037C075EBEBD553F"),
        };

    public SeederLicenseSigner(
        IGlobalSettings globalSettings,
        ILicenseClaimsFactory<User> userLicenseClaimsFactory,
        ILogger<SeederLicenseSigner> logger)
        : this(globalSettings, userLicenseClaimsFactory, logger, _trustedThumbprints)
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

        return LicenseSigningResult.Signed(GenerateToken(certificate, claims, audience));
    }

    /// <summary>
    /// Mirrors <c>LicensingService.GenerateToken</c> (src/Core/Billing/Services/Implementations/LicensingService.cs); keep issuer, algorithm, and lifetime in sync.
    /// </summary>
    private static string GenerateToken(X509Certificate2 certificate, List<Claim> claims, string audience)
    {
        if (claims.All(claim => claim.Type != JwtRegisteredClaimNames.Jti))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
        }

        var rsa = certificate.GetRSAPrivateKey();
        var securityKey = new RsaSecurityKey(rsa);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "bitwarden",
            Audience = audience,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddYears(1),
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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
