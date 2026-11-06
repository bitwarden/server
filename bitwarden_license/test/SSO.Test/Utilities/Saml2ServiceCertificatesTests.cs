using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Bit.Sso.Utilities.Saml2;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Configuration;

namespace Bit.SSO.Test.Utilities;

public class Saml2ServiceCertificatesTests
{
    private const string Password = "Test-Certificate-Password1";

    [Fact]
    public void Build_SameActiveThumbprint_AddsOneCurrentEntryForBoth()
    {
        using var certificate = TestCertificates.Create();

        var entries = Saml2ServiceCertificates.Build(Signing(certificate), Encryption(certificate));

        Assert.Equal([Entry(certificate, CertificateUse.Both, CertificateStatus.Current)], Shape(entries));
    }

    [Fact]
    public void Build_DifferentActiveThumbprints_AddsOneCurrentSigningEntryAndOneCurrentEncryptionEntry()
    {
        using var signing = TestCertificates.Create("CN=Bitwarden Test Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test Encryption");

        var entries = Saml2ServiceCertificates.Build(Signing(signing), Encryption(encryption));

        Assert.Equal(
        [
            Entry(signing, CertificateUse.Signing, CertificateStatus.Current),
            Entry(encryption, CertificateUse.Encryption, CertificateStatus.Current),
        ], Shape(entries));
    }

    [Fact]
    public void Build_NoSigningActive_AddsOnlyTheEncryptionEntry()
    {
        using var encryption = TestCertificates.Create("CN=Bitwarden Test Encryption");

        var entries = Saml2ServiceCertificates.Build(Signing(null), Encryption(encryption));

        Assert.Equal([Entry(encryption, CertificateUse.Encryption, CertificateStatus.Current)], Shape(entries));
    }

    [Fact]
    public void Build_StagedSigning_AddsFutureSigningEntries_UniqueAndNotTheActive()
    {
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");
        using var stagedCopy = X509CertificateLoader.LoadPkcs12(staged.Export(X509ContentType.Pfx, Password), Password);

        var entries = Saml2ServiceCertificates.Build(
            Signing(active, staged: [staged, stagedCopy, active]), Encryption(active));

        Assert.Equal(
        [
            Entry(active, CertificateUse.Both, CertificateStatus.Current),
            Entry(staged, CertificateUse.Signing, CertificateStatus.Future),
        ], Shape(entries));
    }

    [Fact]
    public void Build_NoSigningActive_AddsNoStagedSigningEntry()
    {
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");

        var entries = Saml2ServiceCertificates.Build(Signing(null, staged: [staged]), Encryption(null));

        Assert.Empty(entries);
    }

    [Theory]
    [InlineData("Signing", "SSO signing")]
    [InlineData("Encryption", "SSO encryption")]
    [InlineData("Both", "SSO signing and SSO encryption")]
    public void Build_CertificateRejected_ThrowsNamingTheRoleAndThumbprint(string rejectedUse, string role)
    {
        using var valid = TestCertificates.Create("CN=Bitwarden Test Valid");
        using var rejectedSource = TestCertificates.Create("CN=Bitwarden Test Rejected");
        using var rejected = X509CertificateLoader.LoadCertificate(rejectedSource.RawData);
        var (signing, encryption) = rejectedUse switch
        {
            "Signing" => (rejected, valid),
            "Encryption" => (valid, rejected),
            _ => (rejected, rejected),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Saml2ServiceCertificates.Build(Signing(signing), Encryption(encryption)));

        Assert.Equal(RejectedMessage(role, rejectedSource), exception.Message);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Build_NonRsaEncryptionCertificate_ThrowsNamingTheRoleAndThumbprint()
    {
        using var signing = TestCertificates.Create("CN=Bitwarden Test Signing");
        using var ecdsa = CreateEcdsaCertificate();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Saml2ServiceCertificates.Build(Signing(signing), Encryption(ecdsa)));

        Assert.Equal(RejectedMessage("SSO encryption", ecdsa), exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void Build_StagedAndDecryptionEncryption_AddsUnpublishedCurrentEncryptionEntries_UniqueAndNotTheActive()
    {
        using var signing = TestCertificates.Create("CN=Bitwarden Test Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test Encryption");
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");
        using var decryption = TestCertificates.Create("CN=Bitwarden Test Decryption");

        var entries = Saml2ServiceCertificates.Build(Signing(signing),
            Encryption(encryption, staged: [staged, encryption], decryption: [decryption, staged, encryption]));

        Assert.Equal(
        [
            Entry(signing, CertificateUse.Signing, CertificateStatus.Current),
            Entry(encryption, CertificateUse.Encryption, CertificateStatus.Current),
            Entry(staged, CertificateUse.Encryption, CertificateStatus.Current, MetadataPublishOverrideType.DoNotPublish),
            Entry(decryption, CertificateUse.Encryption, CertificateStatus.Current,
                MetadataPublishOverrideType.DoNotPublish),
        ], Shape(entries));
    }

    [Fact]
    public void Build_NeverAddsAFutureEncryptionEntryOrAValidationCertificate()
    {
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var validation = TestCertificates.Create("CN=Bitwarden Test Validation");
        using var stagedEncryption = TestCertificates.Create("CN=Bitwarden Test Staged Encryption");

        var entries = Saml2ServiceCertificates.Build(Signing(active, validation: [validation]),
            Encryption(active, staged: [stagedEncryption]));

        Assert.Equal(
        [
            Entry(active, CertificateUse.Both, CertificateStatus.Current),
            Entry(stagedEncryption, CertificateUse.Encryption, CertificateStatus.Current,
                MetadataPublishOverrideType.DoNotPublish),
        ], Shape(entries));
    }

    [Fact]
    public void Build_OneSigningCurrent_WhenFallbackPublishesTwoStaged()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");

        var entries = Saml2ServiceCertificates.Build(
            Signing(identity, staged: [identityStaged, ssoStaged], isFallback: true),
            Encryption(identity, staged: [identityStaged], isFallback: true));

        var current = Assert.Single(entries, entry => entry.Status == CertificateStatus.Current &&
            entry.Use is CertificateUse.Both or CertificateUse.Signing);
        Assert.Equal(identity.Thumbprint, current.Certificate.Thumbprint);
        Assert.Equal([identityStaged.Thumbprint, ssoStaged.Thumbprint],
            entries.Where(entry => entry.Status == CertificateStatus.Future).Select(entry => entry.Certificate.Thumbprint));
    }

    [Fact]
    public void GetEncryptionCertificates_Fallback_StagedIncludesIdentityStagedAndSsoStaged()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        using var decryption = TestCertificates.Create("CN=Bitwarden Test Decryption");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.IdentityServer.FutureCertificate = FutureSlot(identityStaged);
        globalSettings.SsoServer.Encryption.FutureCertificate = FutureSlot(ssoStaged);
        globalSettings.SsoServer.Encryption.DecryptionCertificates = [RetiredEntry(decryption)];

        var certificates = Saml2ServiceCertificates.GetEncryptionCertificates(Environment("Production"),
            globalSettings);

        Assert.True(certificates.IsFallback);
        Assert.Equal(identity.Thumbprint, certificates.Active?.Thumbprint);
        Assert.Equal([identityStaged.Thumbprint, ssoStaged.Thumbprint], Thumbprints(certificates.Staged));
        Assert.Equal([decryption.Thumbprint], Thumbprints(certificates.Decryption));
    }

    [Fact]
    public void GetEncryptionCertificates_ActiveSource_StagedIsSsoStagedOnly()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test Encryption");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        using var decryption = TestCertificates.Create("CN=Bitwarden Test Decryption");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.IdentityServer.FutureCertificate = FutureSlot(identityStaged);
        globalSettings.SsoServer.Encryption.CertificateLocation = TestCertificates.WritePfx(encryption, Password);
        globalSettings.SsoServer.Encryption.CertificatePassword = Password;
        globalSettings.SsoServer.Encryption.FutureCertificate = FutureSlot(ssoStaged);
        globalSettings.SsoServer.Encryption.DecryptionCertificates = [RetiredEntry(decryption)];

        var certificates = Saml2ServiceCertificates.GetEncryptionCertificates(Environment("Production"),
            globalSettings);

        Assert.False(certificates.IsFallback);
        Assert.Equal(encryption.Thumbprint, certificates.Active?.Thumbprint);
        Assert.Equal([ssoStaged.Thumbprint], Thumbprints(certificates.Staged));
        Assert.Equal([decryption.Thumbprint], Thumbprints(certificates.Decryption));
    }

    [Fact]
    public void GetEncryptionCertificates_PromotedSlotWithEmptyRoot_UsesTheSlotCertificate()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var promoted = TestCertificates.Create("CN=Bitwarden Test SSO Promoted");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.SsoServer.Encryption.FutureCertificate = FutureSlot(promoted);
        globalSettings.SsoServer.Encryption.FutureCertificate.Active = true;

        var certificates = Saml2ServiceCertificates.GetEncryptionCertificates(Environment("Production"),
            globalSettings);

        Assert.False(certificates.IsFallback);
        Assert.Equal(promoted.Thumbprint, certificates.Active?.Thumbprint);
        Assert.Empty(certificates.Staged);
    }

    [Fact]
    public void GetEncryptionCertificates_Cloud_NoActiveSource_Throws()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.SelfHosted = false;
        globalSettings.SsoServer.Encryption.FutureCertificate = new GlobalSettings.FutureCertificateSettings
        {
            CertificateLocation = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx"),
            CertificatePassword = Password,
            Enabled = true,
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Saml2ServiceCertificates.GetEncryptionCertificates(Environment("Production"), globalSettings));

        Assert.Equal(
            "No SSO encryption certificate is configured. A cloud host must set globalSettings:ssoServer:encryption.",
            exception.Message);
    }

    private static SigningCertificateSet Signing(X509Certificate2? active, X509Certificate2[]? staged = null,
        X509Certificate2[]? validation = null, bool isFallback = false) =>
        new(active, staged ?? [], validation ?? [], isFallback);

    private static EncryptionCertificateSet Encryption(X509Certificate2? active, X509Certificate2[]? staged = null,
        X509Certificate2[]? decryption = null, bool isFallback = false) =>
        new(active, staged ?? [], decryption ?? [], isFallback);

    private static (string Thumbprint, CertificateUse Use, CertificateStatus Status,
        MetadataPublishOverrideType PublishOverride) Entry(X509Certificate2 certificate, CertificateUse use,
        CertificateStatus status, MetadataPublishOverrideType publishOverride = MetadataPublishOverrideType.None) =>
        (certificate.Thumbprint, use, status, publishOverride);

    private static List<(string, CertificateUse, CertificateStatus, MetadataPublishOverrideType)> Shape(
        ServiceCertificateCollection entries) =>
        entries.Select(entry => (entry.Certificate.Thumbprint, entry.Use, entry.Status, entry.MetadataPublishOverride))
            .ToList();

    private static string RejectedMessage(string role, X509Certificate2 certificate) =>
        $"The SAML library rejected the {role} certificate with thumbprint {certificate.Thumbprint}. " +
        "Check that the certificate file or store entry includes its private key, and that an encryption " +
        "certificate has an RSA key.";

    private static X509Certificate2 CreateEcdsaCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest("CN=Bitwarden Test ECDSA", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(now.AddDays(-1), now.AddDays(365));
    }

    private static IEnumerable<string> Thumbprints(IEnumerable<X509Certificate2> certificates) =>
        certificates.Select(certificate => certificate.Thumbprint);

    private static GlobalSettings SelfHostedSettings(X509Certificate2 identity)
    {
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(identity, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
            },
        };
        return globalSettings;
    }

    private static GlobalSettings.FutureCertificateSettings FutureSlot(X509Certificate2 certificate) => new()
    {
        CertificateLocation = TestCertificates.WritePfx(certificate, Password),
        CertificatePassword = Password,
        Enabled = true,
        Active = false,
    };

    private static GlobalSettings.RetiredCertificateSettings RetiredEntry(X509Certificate2 certificate) => new()
    {
        CertificateLocation = TestCertificates.WritePfx(certificate, Password),
        CertificatePassword = Password,
    };

    private static IWebHostEnvironment Environment(string environmentName)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }
}
