using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class IdentityServerCertificateTests
{
    private const string Password = "Test-Certificate-Password1";
    private const string IdentityServerPath = "globalSettings:identityServer";
    private const string IdentityServerFuturePath = "globalSettings:identityServer:futureCertificate";
    private const string IdentityServerValidationPath = "globalSettings:identityServer:validationCertificates";
    private const string SsoServerPath = "globalSettings:ssoServer";
    private const string SsoDecryptionPath = "globalSettings:ssoServer:encryption:decryptionCertificates";

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
    public void SelfHosted_RootFileWithWrongPassword_ThrowsCryptographicException_NoPasswordInExceptionChain()
    {
        const string correctPassword = "Correct-Root-Pfx-Password-7Q3Z";
        const string configuredPassword = "Configured-Root-Pfx-Password-9K4X";
        using var certificate = TestCertificates.Create();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(certificate, correctPassword),
                CertificatePassword = configuredPassword,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;

        var exception = Assert.Throws<CryptographicException>(() =>
            CoreHelpers.GetIdentityServerCertificate(globalSettings));

        Assert.DoesNotContain(configuredPassword, exception.ToString());
        Assert.DoesNotContain(correctPassword, exception.ToString());
    }

    [Fact]
    public void RetiredEntryFileWithWrongPassword_ThrowsWithPath_NoPasswordInExceptionChain()
    {
        const string correctPassword = "Correct-Retired-Pfx-Password-5M8W";
        const string configuredPassword = "Configured-Retired-Pfx-Password-2H6R";
        using var certificate = TestCertificates.Create();
        var globalSettings = new GlobalSettings { SelfHosted = true };
        globalSettings.Storage.ConnectionString = null;
        GlobalSettings.RetiredCertificateSettings[] retired =
        [
            new()
            {
                CertificateLocation = TestCertificates.WritePfx(certificate, correctPassword),
                CertificatePassword = configuredPassword,
            },
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CoreHelpers.GetRetiredCertificates(globalSettings, retired, IdentityServerValidationPath));

        Assert.Contains($"{IdentityServerValidationPath}:0", exception.Message);
        Assert.IsAssignableFrom<CryptographicException>(exception.InnerException);
        Assert.DoesNotContain(configuredPassword, exception.ToString());
        Assert.DoesNotContain(correctPassword, exception.ToString());
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

    [Fact]
    public void FutureEnabledAndActive_IsTheActiveCertificate_RootIgnored()
    {
        using var root = TestCertificates.Create("CN=Bitwarden Test Root");
        using var future = TestCertificates.Create("CN=Bitwarden Test Future");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(root, Password),
                CertificatePassword = "Wrong-Certificate-Password1",
                CertificateThumbprint = null,
                FutureCertificate =
                {
                    CertificateLocation = TestCertificates.WritePfx(future, Password),
                    CertificatePassword = Password,
                    Enabled = true,
                    Active = true,
                },
            },
        };
        var identityServer = globalSettings.IdentityServer;

        using var actual = CoreHelpers.GetActiveCertificate(globalSettings, identityServer,
            identityServer.FutureCertificate, IdentityServerPath, required: true);
        using var staged = CoreHelpers.GetStagedCertificate(globalSettings, identityServer.FutureCertificate,
            IdentityServerFuturePath);

        Assert.NotNull(actual);
        Assert.Equal(future.Thumbprint, actual.Thumbprint);
        Assert.Null(staged);
    }

    [Fact]
    public void FutureEnabledNotActive_IsStaged_RootStillActive()
    {
        using var root = TestCertificates.Create("CN=Bitwarden Test Root");
        using var future = TestCertificates.Create("CN=Bitwarden Test Future");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(root, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
                FutureCertificate =
                {
                    CertificateLocation = TestCertificates.WritePfx(future, Password),
                    CertificatePassword = Password,
                    Enabled = true,
                    Active = false,
                },
            },
        };
        var identityServer = globalSettings.IdentityServer;

        using var actual = CoreHelpers.GetActiveCertificate(globalSettings, identityServer,
            identityServer.FutureCertificate, IdentityServerPath, required: true);
        using var staged = CoreHelpers.GetStagedCertificate(globalSettings, identityServer.FutureCertificate,
            IdentityServerFuturePath);

        Assert.NotNull(actual);
        Assert.Equal(root.Thumbprint, actual.Thumbprint);
        Assert.NotNull(staged);
        Assert.Equal(future.Thumbprint, staged.Thumbprint);
    }

    [Fact]
    public void FutureDisabled_IsNotRead_EvenWhenActiveIsTrue()
    {
        using var root = TestCertificates.Create("CN=Bitwarden Test Root");
        using var future = TestCertificates.Create("CN=Bitwarden Test Future");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(root, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
                FutureCertificate =
                {
                    CertificateLocation = TestCertificates.WritePfx(future, Password),
                    CertificatePassword = "Wrong-Certificate-Password1",
                    Enabled = false,
                    Active = true,
                },
            },
        };
        var identityServer = globalSettings.IdentityServer;

        using var actual = CoreHelpers.GetActiveCertificate(globalSettings, identityServer,
            identityServer.FutureCertificate, IdentityServerPath, required: true);
        using var staged = CoreHelpers.GetStagedCertificate(globalSettings, identityServer.FutureCertificate,
            IdentityServerFuturePath);

        Assert.NotNull(actual);
        Assert.Equal(root.Thumbprint, actual.Thumbprint);
        Assert.Null(staged);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void FutureEnabledButNotLoadable_Throws_MessageHasPathNotPassword(bool active, bool fileExists)
    {
        const string wrongPassword = "Wrong-Certificate-Password1";
        using var root = TestCertificates.Create("CN=Bitwarden Test Root");
        using var future = TestCertificates.Create("CN=Bitwarden Test Future");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(root, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
                FutureCertificate =
                {
                    CertificateLocation = fileExists
                        ? TestCertificates.WritePfx(future, Password)
                        : Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx"),
                    CertificatePassword = wrongPassword,
                    Enabled = true,
                    Active = active,
                },
            },
        };
        globalSettings.Storage.ConnectionString = null;
        var identityServer = globalSettings.IdentityServer;

        var exception = Assert.Throws<InvalidOperationException>(() => active
            ? CoreHelpers.GetActiveCertificate(globalSettings, identityServer, identityServer.FutureCertificate,
                IdentityServerPath, required: false)
            : CoreHelpers.GetStagedCertificate(globalSettings, identityServer.FutureCertificate,
                IdentityServerFuturePath));

        Assert.Contains(IdentityServerFuturePath, exception.Message);
        Assert.Equal(fileExists, exception.InnerException is CryptographicException);
        Assert.DoesNotContain(wrongPassword, exception.ToString());
        Assert.DoesNotContain(Password, exception.ToString());
    }

    [Fact]
    public void RetiredList_ReturnsOnlyEnabledEntries_InOrder()
    {
        using var first = TestCertificates.Create("CN=Bitwarden Test First");
        using var disabled = TestCertificates.Create("CN=Bitwarden Test Disabled");
        using var last = TestCertificates.Create("CN=Bitwarden Test Last");
        var globalSettings = new GlobalSettings { SelfHosted = true };
        GlobalSettings.RetiredCertificateSettings[] retired =
        [
            new() { CertificateLocation = TestCertificates.WritePfx(first, Password), CertificatePassword = Password },
            new()
            {
                CertificateLocation = TestCertificates.WritePfx(disabled, Password),
                CertificatePassword = "Wrong-Certificate-Password1",
                Enabled = false,
            },
            new() { CertificateLocation = TestCertificates.WritePfx(last, Password), CertificatePassword = Password },
        ];

        var actual = CoreHelpers.GetRetiredCertificates(globalSettings, retired, IdentityServerValidationPath);

        Assert.Equal([first.Thumbprint, last.Thumbprint], actual.Select(c => c.Thumbprint));
        foreach (var certificate in actual)
        {
            certificate.Dispose();
        }
    }

    [Fact]
    public void RetiredEnabledWithPasswordOnly_Throws()
    {
        using var loadable = TestCertificates.Create();
        var globalSettings = new GlobalSettings { SelfHosted = true };
        globalSettings.Storage.ConnectionString = null;
        GlobalSettings.RetiredCertificateSettings[] retired =
        [
            new() { CertificateLocation = TestCertificates.WritePfx(loadable, Password), CertificatePassword = Password },
            new() { CertificatePassword = Password },
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CoreHelpers.GetRetiredCertificates(globalSettings, retired, IdentityServerValidationPath));

        Assert.Contains($"{IdentityServerValidationPath}:1", exception.Message);
        Assert.DoesNotContain(Password, exception.ToString());
    }

    [Theory]
    [InlineData("/etc/bitwarden/sso/signing.pfx", null, null, false, false, true)]
    [InlineData(null, "SIGNING-THUMBPRINT", null, false, false, true)]
    [InlineData(null, null, "sso-signing.pfx", false, false, true)]
    [InlineData(null, null, null, true, true, true)]
    [InlineData(null, null, null, false, false, false)]
    [InlineData(null, null, null, false, true, false)]
    [InlineData(null, null, null, true, false, false)]
    [InlineData("replace", "secret", " ", false, false, false)]
    public void HasActiveSource_RootFieldsOrActiveSlot(string? location, string? thumbprint, string? blobName,
        bool futureEnabled, bool futureActive, bool expected)
    {
        var ssoServer = new GlobalSettings.SsoServerSettings
        {
            CertificateLocation = location,
            CertificateThumbprint = thumbprint,
            CertificatePassword = Password,
            BlobName = blobName,
            FutureCertificate =
            {
                CertificateLocation = "/etc/bitwarden/sso/signing-future.pfx",
                CertificatePassword = Password,
                Enabled = futureEnabled,
                Active = futureActive,
            },
        };

        Assert.Equal(expected, CoreHelpers.HasActiveSource(ssoServer, ssoServer.FutureCertificate));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiredRootNotLoadable_Throws(bool fileExists)
    {
        const string ssoServerPath = "globalSettings:ssoServer";
        const string wrongPassword = "Wrong-Certificate-Password1";
        using var root = TestCertificates.Create();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            SsoServer =
            {
                CertificateLocation = fileExists
                    ? TestCertificates.WritePfx(root, Password)
                    : Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx"),
                CertificatePassword = wrongPassword,
            },
        };
        globalSettings.Storage.ConnectionString = null;
        var ssoServer = globalSettings.SsoServer;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CoreHelpers.GetActiveCertificate(globalSettings, ssoServer, ssoServer.FutureCertificate, ssoServerPath,
                required: true));

        Assert.Contains(ssoServerPath, exception.Message);
        Assert.DoesNotContain($"{ssoServerPath}:futureCertificate", exception.Message);
        Assert.Equal(fileExists, exception.InnerException is CryptographicException);
        Assert.DoesNotContain(wrongPassword, exception.ToString());
        Assert.DoesNotContain(Password, exception.ToString());
    }

    [Fact]
    public void IdentityServer_FutureEnabledAndActive_ReturnsTheSlotCertificate()
    {
        using var root = TestCertificates.Create("CN=Bitwarden Test Root");
        using var future = TestCertificates.Create("CN=Bitwarden Test Future");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(root, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
                FutureCertificate =
                {
                    CertificateLocation = TestCertificates.WritePfx(future, Password),
                    CertificatePassword = Password,
                    Enabled = true,
                    Active = true,
                },
            },
        };

        using var actual = CoreHelpers.GetIdentityServerCertificate(globalSettings);

        Assert.NotNull(actual);
        Assert.Equal(future.Thumbprint, actual.Thumbprint);
        Assert.NotEqual(root.Thumbprint, actual.Thumbprint);
    }

    [Fact]
    public void IdentityServer_FutureEnabledAndActiveButNotLoadable_Throws_MessageHasPathNotPassword()
    {
        const string wrongPassword = "Wrong-Certificate-Password1";
        using var root = TestCertificates.Create("CN=Bitwarden Test Root");
        using var future = TestCertificates.Create("CN=Bitwarden Test Future");
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(root, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
                FutureCertificate =
                {
                    CertificateLocation = TestCertificates.WritePfx(future, Password),
                    CertificatePassword = wrongPassword,
                    Enabled = true,
                    Active = true,
                },
            },
        };
        globalSettings.Storage.ConnectionString = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CoreHelpers.GetIdentityServerCertificate(globalSettings));

        Assert.Contains(IdentityServerFuturePath, exception.Message);
        Assert.DoesNotContain(wrongPassword, exception.ToString());
        Assert.DoesNotContain(Password, exception.ToString());
    }

    [Fact]
    public void RetiredEnabledWithPasswordOnly_AfterDisabledEntry_MessageHasConfiguredIndex()
    {
        var globalSettings = new GlobalSettings { SelfHosted = true };
        globalSettings.Storage.ConnectionString = null;
        GlobalSettings.RetiredCertificateSettings[] retired =
        [
            new() { CertificatePassword = Password, Enabled = false },
            new() { CertificatePassword = Password },
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CoreHelpers.GetRetiredCertificates(globalSettings, retired, IdentityServerValidationPath));

        Assert.Contains($"{IdentityServerValidationPath}:1", exception.Message);
        Assert.DoesNotContain($"{IdentityServerValidationPath}:0", exception.Message);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x6E, 0x6F, 0x74, 0x20, 0x61, 0x20, 0x63, 0x65, 0x72, 0x74 })]
    public void FutureEnabledWithUnreadableFile_Throws_MessageHasPath(byte[] content)
    {
        var location = Path.Combine(TestCertificates.CreateSubdirectory(), "future.pfx");
        File.WriteAllBytes(location, content);
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                FutureCertificate =
                {
                    CertificateLocation = location,
                    CertificatePassword = Password,
                    Enabled = true,
                    Active = false,
                },
            },
        };
        globalSettings.Storage.ConnectionString = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CoreHelpers.GetStagedCertificate(globalSettings, globalSettings.IdentityServer.FutureCertificate,
                IdentityServerFuturePath));

        Assert.Contains(IdentityServerFuturePath, exception.Message);
        Assert.NotNull(exception.InnerException);
        Assert.DoesNotContain(Password, exception.ToString());
    }

    [Theory]
    [InlineData(IdentityServerPath)]
    [InlineData(IdentityServerFuturePath)]
    [InlineData($"{IdentityServerValidationPath}:1")]
    [InlineData(SsoServerPath)]
    [InlineData($"{SsoDecryptionPath}:0")]
    public void EcdsaCertificate_InEachRolePosition_ThrowsNamingThePathAndThumbprint(string settingPath)
    {
        using var rsa = TestCertificates.Create("CN=Bitwarden Test RSA");
        using var ecdsa = TestCertificates.CreateEcdsa();
        var ecdsaLocation = TestCertificates.WritePfx(ecdsa, Password);
        var globalSettings = new GlobalSettings { SelfHosted = true };
        globalSettings.Storage.ConnectionString = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LoadFromRolePosition(globalSettings, settingPath, rsa, ecdsaLocation));

        Assert.Equal(
            $"The certificate for {settingPath} with thumbprint {ecdsa.Thumbprint} does not have an RSA key. " +
            "Each Identity signing, SSO signing, and SSO encryption certificate must have an RSA key. " +
            "The host signs tokens with RS256, signs SAML messages with RSA-SHA, " +
            "and decrypts SAML assertions with RSA-OAEP.",
            exception.Message);
        Assert.DoesNotContain(Password, exception.ToString());
        Assert.DoesNotContain(ecdsaLocation, exception.ToString());
    }

    private static object? LoadFromRolePosition(GlobalSettings globalSettings, string settingPath,
        X509Certificate2 rsa, string ecdsaLocation)
    {
        switch (settingPath)
        {
            case IdentityServerPath:
                globalSettings.IdentityServer.CertificateLocation = ecdsaLocation;
                globalSettings.IdentityServer.CertificatePassword = Password;
                globalSettings.IdentityServer.CertificateThumbprint = null;
                return CoreHelpers.GetIdentityServerCertificate(globalSettings);
            case IdentityServerFuturePath:
                var future = globalSettings.IdentityServer.FutureCertificate;
                future.CertificateLocation = ecdsaLocation;
                future.CertificatePassword = Password;
                future.Enabled = true;
                future.Active = false;
                return CoreHelpers.GetStagedCertificate(globalSettings, future, IdentityServerFuturePath);
            case $"{IdentityServerValidationPath}:1":
                return CoreHelpers.GetRetiredCertificates(globalSettings,
                [
                    new() { CertificateLocation = TestCertificates.WritePfx(rsa, Password), CertificatePassword = Password },
                    new() { CertificateLocation = ecdsaLocation, CertificatePassword = Password },
                ], IdentityServerValidationPath);
            case SsoServerPath:
                var ssoServer = globalSettings.SsoServer;
                ssoServer.CertificateLocation = ecdsaLocation;
                ssoServer.CertificatePassword = Password;
                return CoreHelpers.GetActiveCertificate(globalSettings, ssoServer, ssoServer.FutureCertificate,
                    SsoServerPath, required: true);
            case $"{SsoDecryptionPath}:0":
                return CoreHelpers.GetRetiredCertificates(globalSettings,
                    [new() { CertificateLocation = ecdsaLocation, CertificatePassword = Password }], SsoDecryptionPath);
            default:
                throw new ArgumentOutOfRangeException(nameof(settingPath), settingPath, null);
        }
    }
}
