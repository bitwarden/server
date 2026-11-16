using System.Security.Cryptography.X509Certificates;
using Azure.Storage.Blobs;
using Bit.IntegrationTestCommon;
using Bit.SharedWeb.Utilities;
using Bit.Sso.IntegrationTest.Helpers;
using Bit.Sso.Models;
using Bit.Test.Common.Helpers;
using DotNet.Testcontainers.Builders;
using Duende.IdentityServer.Stores;
using Microsoft.IdentityModel.Tokens;
using Sustainsys.Saml2;
using Xunit;

namespace Bit.Sso.IntegrationTest.Certificates;

public class SsoCertificateStartupTests
{
    [Fact]
    public async Task Startup_WithSsoSigningCertificate_SamlAndTokenSigningUseTheSameCertificateObject()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var ssoCertificate = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate)
                     .WithSsoSigningCertificate(ssoCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var samlSigning = Assert.Single(factory.GetService<SamlEnvironment>().ServiceCertificates,
            entry => entry.Use == CertificateUse.Signing).Certificate;
        var tokenSigning = await GetTokenSigningCertificateAsync(factory);

        // Assert
        Assert.Equal(ssoCertificate.Thumbprint, samlSigning.Thumbprint);
        Assert.Same(samlSigning, tokenSigning);
        Assert.Same(samlSigning, GetReport(factory, CertificateRole.SsoSigning).Active);
    }

    [Fact]
    public async Task Startup_FallbackForBothSsoRoles_SharesTheIdentityCertificateObject()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var encryption = GetReport(factory, CertificateRole.SsoEncryption);
        var signing = GetReport(factory, CertificateRole.SsoSigning);
        var samlEntry = Assert.Single(factory.GetService<SamlEnvironment>().ServiceCertificates);
        var tokenSigning = await GetTokenSigningCertificateAsync(factory);

        // Assert
        Assert.True(encryption.IsFallback);
        Assert.True(signing.IsFallback);
        Assert.Equal(identityCertificate.Thumbprint, encryption.Active?.Thumbprint);
        Assert.Same(encryption.Active, signing.Active);
        Assert.Equal(CertificateUse.Both, samlEntry.Use);
        Assert.Same(signing.Active, samlEntry.Certificate);
        Assert.Same(signing.Active, tokenSigning);
    }

    [Fact]
    public void Startup_ProductionWithNoSsoServer_FailsOnSsoEncryptionFirst()
    {
        // Arrange
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Production",
            "No SSO encryption certificate is configured.");

        // Assert
        Assert.Equal(
            "No SSO encryption certificate is configured. A cloud host must set globalSettings:ssoServer:encryption.",
            exception.Message);
    }

    [Fact]
    public async Task Startup_ProductionWithSsoEncryptionBlobAndNoSsoSigningSource_FailsOnSsoSigning()
    {
        // Arrange
        await using var azurite = new ContainerBuilder(TestContainerImages.Azurite)
            .WithPortBinding(10000, true)
            .Build();
        await azurite.StartAsync();
        var connectionString =
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            $"BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var certificates = (await new BlobServiceClient(connectionString)
            .CreateBlobContainerAsync("certificates")).Value;
        await certificates.UploadBlobAsync("sso-encryption.pfx", new BinaryData(
            encryption.Export(X509ContentType.Pfx, CertificateTestConfiguration.CertificatePassword)));

        using var factory = new SsoApplicationFactory();
        var configuration = CertificateTestConfiguration.NoCertificate();
        configuration["globalSettings:storage:connectionString"] = connectionString;
        configuration["globalSettings:dataProtection:keyProtectionPolicy"] = "StorageManaged";
        configuration["globalSettings:ssoServer:encryption:blobName"] = "sso-encryption.pfx";
        configuration["globalSettings:ssoServer:encryption:certificatePassword"] =
            CertificateTestConfiguration.CertificatePassword;
        foreach (var (key, value) in configuration)
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Production",
            "No SSO signing certificate is configured.");

        // Assert
        Assert.Equal("No SSO signing certificate is configured. A cloud host must set globalSettings:ssoServer.",
            exception.Message);
    }

    [Fact]
    public void Startup_ProductionWithNoSsoServerEncryption_Fails()
    {
        // Arrange
        using var factory = new SsoApplicationFactory();
        var configuration = CertificateTestConfiguration.NoCertificate();
        configuration["globalSettings:ssoServer:blobName"] = "sso.pfx";
        foreach (var (key, value) in configuration)
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act & Assert
        StartupTestHelpers.AssertStartupFails(factory, "Production",
            "No SSO encryption certificate is configured. A cloud host must set globalSettings:ssoServer:encryption.");
    }

    [Fact]
    public void Startup_EncryptionCertificateWithNoPrivateKey_Fails()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var publicOnly = X509CertificateLoader.LoadCertificate(encryption.RawData);
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoEncryptionCertificate(publicOnly))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "SSO encryption");

        // Assert
        Assert.Contains(encryption.Thumbprint, exception.Message);
        Assert.Contains("does not contain a private key", exception.InnerException?.Message);
    }

    [Fact]
    public void Startup_LogsCertificateSources()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var logs = new CollectingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoSigningCertificate(signing)
                     .WithSsoEncryptionCertificate(encryption))
        {
            factory.UpdateConfiguration(key, value);
        }
        factory.ConfigureServices(services => services.AddSingleton<ILoggerFactory>(loggerFactory));

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.Equal(
            [
                ("SSO encryption", "SsoServer", encryption.Thumbprint),
                ("SSO signing", "SsoServer", signing.Thumbprint),
            ],
            StartupTestHelpers.GetCertificateSourceEntries(logs)
                .Select(entry => (entry.Values["Role"], entry.Values["Source"], entry.Values["ActiveThumbprint"]))
                .OrderBy(entry => entry.Item1));
    }

    [Fact]
    public void Startup_FallbackWithStagedAndDecryptionCertificates_LogsEachCertificateInItsField()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signingStaged = TestCertificates.Create("CN=Bitwarden Test SSO Signing Staged");
        using var encryptionStaged = TestCertificates.Create("CN=Bitwarden Test SSO Encryption Staged");
        using var decryption = TestCertificates.Create("CN=Bitwarden Test SSO Decryption");
        var logs = new CollectingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoStagedSigningCertificate(signingStaged)
                     .WithSsoEncryptionFutureCertificate(encryptionStaged, active: false)
                     .WithSsoDecryptionCertificates(decryption))
        {
            factory.UpdateConfiguration(key, value);
        }
        factory.ConfigureServices(services => services.AddSingleton<ILoggerFactory>(loggerFactory));

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.Equal(
            [
                ("SSO encryption", "Identity fallback", identity.Thumbprint,
                    encryptionStaged.Thumbprint, decryption.Thumbprint),
                ("SSO signing", "Identity fallback", identity.Thumbprint,
                    signingStaged.Thumbprint, "none"),
            ],
            StartupTestHelpers.GetCertificateSourceEntries(logs)
                .Select(entry => (entry.Values["Role"], entry.Values["Source"], entry.Values["ActiveThumbprint"],
                    entry.Values["StagedThumbprints"], entry.Values["RetiredThumbprints"]))
                .OrderBy(entry => entry.Item1));
    }

    [Fact]
    public void Startup_WithEcdsaSsoSigningCertificate_FailsNamingTheSsoRoot()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.CreateEcdsa("CN=Bitwarden Test SSO Signing ECDSA");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoSigningCertificate(signing)
                     .WithSsoEncryptionCertificate(encryption))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "does not have an RSA key");

        // Assert
        Assert.Equal(StartupTestHelpers.NoRsaKeyMessage("globalSettings:ssoServer", signing.Thumbprint),
            exception.Message);
    }

    [Fact]
    public void Startup_WithEcdsaSsoEncryptionCertificate_FailsNamingTheEncryptionRoot()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.CreateEcdsa("CN=Bitwarden Test SSO Encryption ECDSA");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoEncryptionCertificate(encryption))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "does not have an RSA key");

        // Assert
        Assert.Equal(StartupTestHelpers.NoRsaKeyMessage("globalSettings:ssoServer:encryption", encryption.Thumbprint),
            exception.Message);
    }

    private static async Task<X509Certificate2> GetTokenSigningCertificateAsync(SsoApplicationFactory factory)
    {
        var credential = await factory.GetService<ISigningCredentialStore>().GetSigningCredentialsAsync();
        return Assert.IsType<X509SecurityKey>(credential.Key).Certificate;
    }

    private static CertificateSourceReport GetReport(SsoApplicationFactory factory, CertificateRole role) =>
        Assert.Single(factory.Services.GetServices<CertificateSourceReport>(), report => report.Role == role);
}
