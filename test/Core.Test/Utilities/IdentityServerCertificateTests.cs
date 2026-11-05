using System.Security.Cryptography;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class IdentityServerCertificateTests
{
    private const string Password = "Test-Certificate-Password1";

    [Fact]
    public void SelfHosted_WithPasswordAndExistingFile_LoadsTheFile()
    {
        using var expected = TestCertificates.Create();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(expected, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
            },
        };

        using var actual = CoreHelpers.GetIdentityServerCertificate(globalSettings);

        Assert.NotNull(actual);
        Assert.Equal(expected.Thumbprint, actual.Thumbprint);
        Assert.True(actual.HasPrivateKey);
    }

    [Fact]
    public void SelfHosted_WithPasswordAndMissingFile_ReturnsNull()
    {
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx"),
                CertificatePassword = Password,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public void Cloud_WithPasswordAndExistingFile_IgnoresTheFile()
    {
        using var certificate = TestCertificates.Create();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = false,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(certificate, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("secret")]
    [InlineData("SECRET")]
    [InlineData("replace")]
    public void SelfHosted_WithPlaceholderOrEmptyPassword_IgnoresTheFile(string password)
    {
        using var certificate = TestCertificates.Create();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(certificate, password),
                CertificatePassword = password,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public void SelfHosted_FileWithWrongPassword_ThrowsAndDoesNotFallThrough()
    {
        using var certificate = TestCertificates.Create();
        using var other = TestCertificates.Create("CN=Bitwarden Test Thumbprint");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(certificate, Password),
                CertificatePassword = "Wrong-Certificate-Password1",
                CertificateThumbprint = other.Thumbprint,
            },
        };
        globalSettings.Storage.ConnectionString = null;

        Assert.Throws<CryptographicException>(() => CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public void NoCertificateSettings_ReturnsNull()
    {
        var globalSettings = new GlobalSettings
        {
            SelfHosted = false,
            IdentityServer =
            {
                CertificateLocation = "identity.pfx",
                CertificatePassword = null,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public void Thumbprint_NotInStore_ReturnsNull()
    {
        using var unstored = TestCertificates.Create();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = false,
            IdentityServer =
            {
                CertificatePassword = null,
                CertificateThumbprint = unstored.Thumbprint,
            },
        };
        globalSettings.Storage.ConnectionString = null;

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public void SelfHosted_FileAndThumbprintBothSet_FileWins()
    {
        using var fromFile = TestCertificates.Create("CN=Bitwarden Test File");
        using var other = TestCertificates.Create("CN=Bitwarden Test Thumbprint");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(fromFile, Password),
                CertificatePassword = Password,
                CertificateThumbprint = other.Thumbprint,
            },
        };

        using var actual = CoreHelpers.GetIdentityServerCertificate(globalSettings);

        Assert.NotNull(actual);
        Assert.Equal(fromFile.Thumbprint, actual.Thumbprint);
        Assert.NotEqual(other.Thumbprint, actual.Thumbprint);
    }
}
