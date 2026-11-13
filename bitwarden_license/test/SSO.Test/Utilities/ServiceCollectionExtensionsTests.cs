using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Bit.Sso.Utilities;
using Bit.Sso.Utilities.Saml2;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SSO.Test.Utilities;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSsoServices_EncryptionCertificateWithNoPrivateKey_ThrowsBeforeTheServiceProviderIsBuilt()
    {
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var publicOnly = X509CertificateLoader.LoadCertificate(encryption.RawData);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSsoServices(new GlobalSettings(), Signing(signing), Encryption(publicOnly)));

        Assert.Contains($"SSO encryption certificate with thumbprint {encryption.Thumbprint}", exception.Message);
    }

    [Fact]
    public void AddSsoServices_SigningCertificateWithNoPrivateKey_ThrowsBeforeTheServiceProviderIsBuilt()
    {
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var publicOnly = X509CertificateLoader.LoadCertificate(signing.RawData);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSsoServices(new GlobalSettings(), Signing(publicOnly), Encryption(encryption)));

        Assert.Contains($"SSO signing certificate with thumbprint {signing.Thumbprint}", exception.Message);
    }

    private static SigningCertificateSet Signing(X509Certificate2 active) => new(active, [], [], IsFallback: false);

    private static EncryptionCertificateSet Encryption(X509Certificate2 active) =>
        new(active, [], [], IsFallback: false);
}
