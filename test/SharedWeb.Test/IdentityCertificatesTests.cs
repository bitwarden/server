using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Bit.Test.Common.Helpers;

namespace Bit.SharedWeb.Test;

public class IdentityCertificatesTests
{
    private const string Password = "Test-Certificate-Password1";

    [Fact]
    public void ReadTwice_ReturnsTheSameObjects()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var identityRetired = TestCertificates.Create("CN=Bitwarden Test Identity Retired");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.IdentityServer.FutureCertificate = new GlobalSettings.FutureCertificateSettings
        {
            CertificateLocation = TestCertificates.WritePfx(identityStaged, Password),
            CertificatePassword = Password,
            Enabled = true,
            Active = false,
        };
        globalSettings.IdentityServer.ValidationCertificates =
        [
            new GlobalSettings.RetiredCertificateSettings
            {
                CertificateLocation = TestCertificates.WritePfx(identityRetired, Password),
                CertificatePassword = Password,
            },
        ];
        var certificates = new IdentityCertificates(globalSettings);

        var active = certificates.Active;
        var staged = certificates.Staged;
        var validation = certificates.Validation;

        Assert.Equal(identity.Thumbprint, active?.Thumbprint);
        Assert.Equal(identityStaged.Thumbprint, Assert.Single(staged).Thumbprint);
        Assert.Equal(identityRetired.Thumbprint, Assert.Single(validation).Thumbprint);
        Assert.Same(active, certificates.Active);
        Assert.Same(staged, certificates.Staged);
        Assert.Same(validation, certificates.Validation);
    }

    [Fact]
    public void ValidationNotRead_UnloadableValidationEntry_DoesNotThrow()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.IdentityServer.ValidationCertificates =
        [
            new GlobalSettings.RetiredCertificateSettings
            {
                CertificateLocation = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx"),
                CertificatePassword = Password,
            },
        ];
        var certificates = new IdentityCertificates(globalSettings);

        Assert.Equal(identity.Thumbprint, certificates.Active?.Thumbprint);
        Assert.Empty(certificates.Staged);
        var exception = Assert.Throws<InvalidOperationException>(() => certificates.Validation);
        Assert.Contains("globalSettings:identityServer:validationCertificates:0 ", exception.Message);
    }

    private static GlobalSettings SelfHostedSettings(X509Certificate2 identity)
    {
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            DevelopmentDirectory = null,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(identity, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;
        return globalSettings;
    }
}
