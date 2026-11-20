using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Identity.IntegrationTest.Certificates;

public class IdentityCertificateStartupTests
{
    [Fact]
    public void Startup_LogsCertificateSources()
    {
        // Arrange
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");
        var logs = new CollectingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var factory = CertificateTestHelpers.CreateFactory(CertificateTestConfiguration.SelfHosted(active, staged, futureActive: false));
        factory.ConfigureServices(services => services.AddSingleton<ILoggerFactory>(loggerFactory));

        // Act
        using var client = factory.CreateClient();

        // Assert
        var entry = Assert.Single(StartupTestHelpers.GetCertificateSourceEntries(logs));
        Assert.Equal("Identity signing", entry.Values["Role"]);
        Assert.Equal("IdentityServer", entry.Values["Source"]);
        Assert.Equal(active.Thumbprint, entry.Values["ActiveThumbprint"]);
        Assert.Equal(staged.Thumbprint, entry.Values["StagedThumbprints"]);
    }

    [Fact]
    public void Startup_WithEcdsaIdentityCertificate_FailsNamingTheRoot()
    {
        // Arrange
        using var certificate = TestCertificates.CreateEcdsa();
        using var factory = CertificateTestHelpers.CreateFactory(CertificateTestConfiguration.SelfHosted(certificate));

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "does not have an RSA key");

        // Assert
        Assert.Equal(StartupTestHelpers.NoRsaKeyMessage("globalSettings:identityServer", certificate.Thumbprint),
            exception.Message);
    }

    [Fact]
    public void Startup_WithRsaActiveAndEcdsaStagedCertificate_FailsNamingTheFutureSlot()
    {
        // Arrange
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var staged = TestCertificates.CreateEcdsa("CN=Bitwarden Test Staged ECDSA");
        using var factory = CertificateTestHelpers.CreateFactory(
            CertificateTestConfiguration.SelfHosted(active, staged, futureActive: false));

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "does not have an RSA key");

        // Assert
        Assert.Equal(
            StartupTestHelpers.NoRsaKeyMessage("globalSettings:identityServer:futureCertificate", staged.Thumbprint),
            exception.Message);
    }

    [Fact]
    public void Startup_WithEcdsaValidationCertificate_FailsNamingTheRetiredEntry()
    {
        // Arrange
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");
        using var validation = TestCertificates.CreateEcdsa("CN=Bitwarden Test Validation ECDSA");
        using var factory = CertificateTestHelpers.CreateFactory(
            CertificateTestConfiguration.SelfHosted(active, staged, futureActive: false, validation));

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "does not have an RSA key");

        // Assert
        Assert.Equal(
            StartupTestHelpers.NoRsaKeyMessage("globalSettings:identityServer:validationCertificates:0",
                validation.Thumbprint),
            exception.Message);
    }
}
