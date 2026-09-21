using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Seeder.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Services;

/// <summary>Guards JWT parity between <see cref="SeederLicenseSigner"/> and production code <c>LicensingService.GenerateToken</c>, and covers the skip branches.</summary>
public sealed class SeederLicenseSignerTests : IDisposable
{
    private const string _password = "test-cert-password";

    private readonly string _certPath = Path.Join(Path.GetTempPath(), $"seeder-signing-{Guid.NewGuid():N}.pfx");
    private readonly X509Certificate2 _certificate;

    public SeederLicenseSignerTests()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Seeder License Signing Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        _certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        File.WriteAllBytes(_certPath, _certificate.Export(X509ContentType.Pfx, _password));
    }

    public void Dispose()
    {
        _certificate.Dispose();
        if (File.Exists(_certPath))
        {
            File.Delete(_certPath);
        }
    }

    [Fact]
    public async Task CreateUserTokenAsync_CertificateConfigured_MintsTokenMatchingLicensingServiceShape()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "premium.user@example.com", Premium = true };
        using var signer = NewSigner();

        var result = await signer.CreateUserTokenAsync(user);

        Assert.Null(result.Warning);
        Assert.NotNull(result.Token);

        var handler = new JwtSecurityTokenHandler();
        var expectedAudience = $"user:{user.Id}";

        handler.ValidateToken(result.Token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "bitwarden",
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new X509SecurityKey(_certificate),
        }, out var validated);

        var jwt = Assert.IsType<JwtSecurityToken>(validated);
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);

        Assert.True(jwt.ValidTo >= DateTime.UtcNow.AddYears(1).AddDays(-1));
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddYears(1).AddDays(1));

        Assert.Contains(jwt.Claims, c => c.Type == nameof(UserLicenseConstants.Id) && c.Value == user.Id.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == nameof(UserLicenseConstants.Premium) && c.Value == "True");
    }

    [Fact]
    public async Task CreateUserTokenAsync_CalledTwice_SignsBothTimes()
    {
        using var signer = NewSigner();

        var first = await signer.CreateUserTokenAsync(LicenseTestHelpers.NewPremiumOwner());
        var second = await signer.CreateUserTokenAsync(LicenseTestHelpers.NewPremiumOwner());

        Assert.NotNull(first.Token);
        Assert.NotNull(second.Token);
    }

    [Fact]
    public async Task CreateUserTokenAsync_NoCertificateConfigured_SkipsWithWarning()
    {
        using var signer = NewSigner(path: string.Empty, password: string.Empty);

        var result = await signer.CreateUserTokenAsync(LicenseTestHelpers.NewPremiumOwner());

        Assert.Null(result.Token);
        Assert.False(string.IsNullOrEmpty(result.Warning));
    }

    [Fact]
    public async Task CreateUserTokenAsync_CertificateFileMissing_SkipsWithWarning()
    {
        var missingPath = Path.Join(Path.GetTempPath(), $"seeder-missing-{Guid.NewGuid():N}.pfx");
        using var signer = NewSigner(path: missingPath);

        var result = await signer.CreateUserTokenAsync(LicenseTestHelpers.NewPremiumOwner());

        Assert.Null(result.Token);
        Assert.False(string.IsNullOrEmpty(result.Warning));
    }

    [Fact]
    public async Task CreateUserTokenAsync_CertificateLoadFails_SkipsWithWarningThatDoesNotLeakDetail()
    {
        using var signer = NewSigner(password: "wrong-password");

        var result = await signer.CreateUserTokenAsync(LicenseTestHelpers.NewPremiumOwner());

        Assert.Null(result.Token);
        Assert.False(string.IsNullOrEmpty(result.Warning));
        Assert.DoesNotContain(_certPath, result.Warning);
    }

    [Fact]
    public async Task CreateUserTokenAsync_CertificateHasNoPrivateKey_SkipsWithWarning()
    {
        var publicOnlyPath = Path.Join(Path.GetTempPath(), $"seeder-public-{Guid.NewGuid():N}.cer");
        File.WriteAllBytes(publicOnlyPath, _certificate.Export(X509ContentType.Cert));
        try
        {
            using var signer = NewSigner(path: publicOnlyPath, allowed: AllowedFor(_certificate));

            var result = await signer.CreateUserTokenAsync(LicenseTestHelpers.NewPremiumOwner());

            Assert.Null(result.Token);
            Assert.Contains("no RSA private key", result.Warning);
        }
        finally
        {
            File.Delete(publicOnlyPath);
        }
    }

    [Fact]
    public async Task CreateUserTokenAsync_UntrustedThumbprint_SkipsWithWarning()
    {
        var globalSettings = new GlobalSettings
        {
            LicenseCertificatePath = _certPath,
            LicenseCertificatePassword = _password,
        };
        using var signer = new SeederLicenseSigner(
            globalSettings, new UserLicenseClaimsFactory(), NullLogger<SeederLicenseSigner>.Instance);

        var result = await signer.CreateUserTokenAsync(LicenseTestHelpers.NewPremiumOwner());

        Assert.Null(result.Token);
        Assert.False(string.IsNullOrEmpty(result.Warning));
    }

    private static IReadOnlySet<string> AllowedFor(X509Certificate2 certificate) =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CoreHelpers.CleanCertificateThumbprint(certificate.Thumbprint),
        };

    private SeederLicenseSigner NewSigner(
        string? path = null, string? password = null, IReadOnlySet<string>? allowed = null)
    {
        var globalSettings = new GlobalSettings
        {
            LicenseCertificatePath = path ?? _certPath,
            LicenseCertificatePassword = password ?? _password,
        };

        return new SeederLicenseSigner(
            globalSettings,
            new UserLicenseClaimsFactory(),
            NullLogger<SeederLicenseSigner>.Instance,
            allowed ?? AllowedFor(_certificate));
    }
}
