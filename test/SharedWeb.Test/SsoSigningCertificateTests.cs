using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Bit.Test.Common.Helpers;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bit.SharedWeb.Test;

public class SsoSigningCertificateTests : IClassFixture<IdentityServerCertificateTests.AzuriteFixture>
{
    private const string Password = "Test-Certificate-Password1";

    private readonly IdentityServerCertificateTests.AzuriteFixture _azurite;

    public SsoSigningCertificateTests(IdentityServerCertificateTests.AzuriteFixture azurite)
    {
        _azurite = azurite;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSsoSigningCertificates_ActiveSource_UsesTheSsoCertificate(bool promotedSlot)
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var sso = TestCertificates.Create("CN=Bitwarden Test SSO");
        var globalSettings = SelfHostedSettings(identity);
        if (promotedSlot)
        {
            globalSettings.SsoServer.FutureCertificate = FutureSlot(sso, active: true);
        }
        else
        {
            SetRoot(globalSettings.SsoServer, sso);
        }

        var certificates = GetCertificates("Production", globalSettings);

        Assert.Equal(sso.Thumbprint, certificates.Active?.Thumbprint);
        Assert.False(certificates.IsFallback);
    }

    [Theory]
    [InlineData(true, "Production")]
    [InlineData(false, "Development")]
    public void GetSsoSigningCertificates_NoActiveSourceSelfHostedOrDevelopment_FallsBackToIdentity(
        bool selfHosted, string environmentName)
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.SelfHosted = selfHosted;

        var certificates = GetCertificates(environmentName, globalSettings);

        Assert.True(certificates.IsFallback);
        Assert.Equal(selfHosted ? identity.Thumbprint : null, certificates.Active?.Thumbprint);
    }

    [Theory]
    [InlineData(false, "globalSettings:ssoServer")]
    [InlineData(true, "globalSettings:ssoServer:futureCertificate")]
    public void GetSsoSigningCertificates_SourceSetButNotLoadable_ThrowsNamingThePath(bool promotedSlot,
        string expectedPath)
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        var globalSettings = SelfHostedSettings(identity);
        var missingLocation = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx");
        if (promotedSlot)
        {
            globalSettings.SsoServer.FutureCertificate = new GlobalSettings.FutureCertificateSettings
            {
                CertificateLocation = missingLocation,
                CertificatePassword = Password,
                Enabled = true,
                Active = true,
            };
        }
        else
        {
            globalSettings.SsoServer.CertificateLocation = missingLocation;
            globalSettings.SsoServer.CertificatePassword = Password;
        }

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCertificates("Production", globalSettings));

        Assert.Contains($"{expectedPath} ", exception.Message);
        Assert.DoesNotContain(Password, exception.Message);
    }

    [Fact]
    public void GetSsoSigningCertificates_CloudNoActiveSourceWithUnloadableStagedSlot_ThrowsTheCloudMessage()
    {
        var globalSettings = NoCertificateSettings();
        globalSettings.SsoServer.FutureCertificate = MissingFutureSlot();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCertificates("Production", globalSettings));

        Assert.StartsWith("No SSO signing certificate is configured.", exception.Message);
        Assert.Contains("globalSettings:ssoServer", exception.Message);
        Assert.DoesNotContain("globalSettings:ssoServer:futureCertificate", exception.Message);
    }

    [Theory]
    [InlineData("globalSettings:ssoServer:futureCertificate")]
    [InlineData("globalSettings:ssoServer:validationCertificates:0")]
    public void GetSsoSigningCertificates_FallbackWithUnloadableSsoSlot_ThrowsNamingTheSlot(string expectedPath)
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        var globalSettings = SelfHostedSettings(identity);
        if (expectedPath.EndsWith("futureCertificate"))
        {
            globalSettings.SsoServer.FutureCertificate = MissingFutureSlot();
        }
        else
        {
            globalSettings.SsoServer.ValidationCertificates =
            [
                new GlobalSettings.RetiredCertificateSettings
                {
                    CertificateLocation = MissingLocation(),
                    CertificatePassword = Password,
                },
            ];
        }

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCertificates("Production", globalSettings));

        Assert.Contains($"{expectedPath} ", exception.Message);
        Assert.DoesNotContain(Password, exception.ToString());
    }

    [Fact]
    public void GetSsoSigningCertificates_ActiveSource_IgnoresIdentitySlots()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var identityRetired = TestCertificates.Create("CN=Bitwarden Test Identity Retired");
        using var sso = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        using var ssoRetired = TestCertificates.Create("CN=Bitwarden Test SSO Retired");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.IdentityServer.FutureCertificate = FutureSlot(identityStaged, active: false);
        globalSettings.IdentityServer.ValidationCertificates = [RetiredEntry(identityRetired)];
        SetRoot(globalSettings.SsoServer, sso);
        globalSettings.SsoServer.FutureCertificate = FutureSlot(ssoStaged, active: false);
        globalSettings.SsoServer.ValidationCertificates = [RetiredEntry(ssoRetired)];

        var certificates = GetCertificates("Production", globalSettings);

        Assert.Equal(sso.Thumbprint, certificates.Active?.Thumbprint);
        Assert.Equal(ssoStaged.Thumbprint, Assert.Single(certificates.Staged).Thumbprint);
        Assert.Equal(ssoRetired.Thumbprint, Assert.Single(certificates.Validation).Thumbprint);
    }

    [Fact]
    public void GetSsoSigningCertificates_Fallback_IncludesIdentityStagedAndValidation_AndSsoSlots()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var identityRetired = TestCertificates.Create("CN=Bitwarden Test Identity Retired");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        using var ssoRetired = TestCertificates.Create("CN=Bitwarden Test SSO Retired");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.IdentityServer.FutureCertificate = FutureSlot(identityStaged, active: false);
        globalSettings.IdentityServer.ValidationCertificates = [RetiredEntry(identityRetired)];
        globalSettings.SsoServer.FutureCertificate = FutureSlot(ssoStaged, active: false);
        globalSettings.SsoServer.ValidationCertificates = [RetiredEntry(ssoRetired)];

        var certificates = GetCertificates("Production", globalSettings);

        Assert.True(certificates.IsFallback);
        Assert.Equal(identity.Thumbprint, certificates.Active?.Thumbprint);
        Assert.Equivalent(new[] { identityStaged.Thumbprint, ssoStaged.Thumbprint },
            certificates.Staged.Select(certificate => certificate.Thumbprint), strict: true);
        Assert.Equivalent(new[] { identityRetired.Thumbprint, ssoRetired.Thumbprint },
            certificates.Validation.Select(certificate => certificate.Thumbprint), strict: true);
    }

    [Fact]
    public async Task GetSsoSigningCertificates_CloudWithSsoBlob_UsesTheBlob()
    {
        using var expected = TestCertificates.Create();
        await _azurite.ResetAsync(expected, Password);
        var globalSettings = NoCertificateSettings();
        globalSettings.Storage.ConnectionString = _azurite.ConnectionString;
        globalSettings.SsoServer.BlobName = "identity.pfx";
        globalSettings.SsoServer.CertificatePassword = Password;

        var certificates = GetCertificates("Production", globalSettings);

        Assert.Equal(expected.Thumbprint, certificates.Active?.Thumbprint);
        Assert.False(certificates.IsFallback);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void GetSsoSigningCertificates_CloudNoActiveSource_ThrowsNamingSsoServer(string environmentName)
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.SelfHosted = false;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCertificates(environmentName, globalSettings));

        Assert.Contains("globalSettings:ssoServer", exception.Message);
        Assert.DoesNotContain(Password, exception.Message);
    }

    [Fact]
    public async Task AddSsoSigningCertificate_ActiveSource_SsoSigns_SsoSlotsArePublished()
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var sso = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        using var ssoRetired = TestCertificates.Create("CN=Bitwarden Test SSO Retired");
        var globalSettings = SelfHostedSettings(identity);
        globalSettings.IdentityServer.FutureCertificate = FutureSlot(identityStaged, active: false);
        SetRoot(globalSettings.SsoServer, sso);
        globalSettings.SsoServer.FutureCertificate = FutureSlot(ssoStaged, active: false);
        globalSettings.SsoServer.ValidationCertificates = [RetiredEntry(ssoRetired)];

        using var provider = BuildProvider("Production", globalSettings);

        var credential = await provider.GetRequiredService<ISigningCredentialStore>().GetSigningCredentialsAsync();
        Assert.Equal(KeyId(sso), credential.Kid);
        Assert.Equivalent(new[] { KeyId(sso), KeyId(ssoStaged), KeyId(ssoRetired) },
            await GetPublishedKeyIdsAsync(provider), strict: true);
    }

    [Fact]
    public async Task AddSsoSigningCertificate_DevelopmentWithNoCertificate_RegistersADeveloperKey()
    {
        var globalSettings = NoCertificateSettings();

        using var provider = BuildProvider("Development", globalSettings);

        var credential = await provider.GetRequiredService<ISigningCredentialStore>().GetSigningCredentialsAsync();
        Assert.NotNull(credential);
        Assert.DoesNotContain("RS256", credential.Kid);
    }

    [Fact]
    public void AddSsoSigningCertificate_SelfHostedProductionWithNoCertificate_Throws()
    {
        var globalSettings = NoCertificateSettings();
        globalSettings.SelfHosted = true;

        var exception = Assert.Throws<Exception>(() => BuildProvider("Production", globalSettings));

        Assert.Equal("No identity certificate to use.", exception.Message);
    }

    [Fact]
    public void AddSsoSigningCertificate_SigningCertificateWithNoPrivateKey_ThrowsBeforeTheServiceProviderIsBuilt()
    {
        using var sso = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var publicOnly = X509CertificateLoader.LoadCertificate(sso.RawData);
        var builder = new ServiceCollection().AddIdentityServer();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddSsoSigningCertificate(Environment("Production"), NoCertificateSettings(),
                new SigningCertificateSet(publicOnly, [], [], IsFallback: false)));

        Assert.Equal("X509 certificate does not have a private key.", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddSsoSigningCertificate_RegistersOneSsoSigningReport(bool fallback)
    {
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var sso = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        using var ssoRetired = TestCertificates.Create("CN=Bitwarden Test SSO Retired");
        var globalSettings = SelfHostedSettings(identity);
        if (!fallback)
        {
            SetRoot(globalSettings.SsoServer, sso);
        }
        globalSettings.SsoServer.FutureCertificate = FutureSlot(ssoStaged, active: false);
        globalSettings.SsoServer.ValidationCertificates = [RetiredEntry(ssoRetired)];

        using var provider = BuildProvider("Production", globalSettings);

        var report = Assert.Single(provider.GetServices<CertificateSourceReport>());
        Assert.Equal(CertificateRole.SsoSigning, report.Role);
        Assert.Equal(fallback ? identity.Thumbprint : sso.Thumbprint, report.Active?.Thumbprint);
        Assert.Equal(ssoStaged.Thumbprint, Assert.Single(report.Staged).Thumbprint);
        Assert.Equal(ssoRetired.Thumbprint, Assert.Single(report.Retired).Thumbprint);
        Assert.Equal(fallback, report.IsFallback);
    }

    private static GlobalSettings NoCertificateSettings()
    {
        var globalSettings = new GlobalSettings
        {
            SelfHosted = false,
            DevelopmentDirectory = null,
            IdentityServer =
            {
                CertificateLocation = null,
                CertificatePassword = null,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;
        return globalSettings;
    }

    private static SigningCertificateSet GetCertificates(string environmentName, GlobalSettings globalSettings) =>
        SharedWeb.Utilities.ServiceCollectionExtensions.GetSsoSigningCertificates(Environment(environmentName),
            globalSettings, new IdentityCertificates(globalSettings));

    private static string KeyId(X509Certificate2 certificate) => certificate.Thumbprint + "RS256";

    private static async Task<List<string>> GetPublishedKeyIdsAsync(ServiceProvider provider)
    {
        var keyIds = new List<string>();
        foreach (var store in provider.GetServices<IValidationKeysStore>())
        {
            keyIds.AddRange((await store.GetValidationKeysAsync()).Select(key => key.Key.KeyId));
        }
        return keyIds;
    }

    private static ServiceProvider BuildProvider(string environmentName, GlobalSettings globalSettings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServer().AddSsoSigningCertificate(Environment(environmentName), globalSettings,
            GetCertificates(environmentName, globalSettings));
        return services.BuildServiceProvider();
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

    private static void SetRoot(ICertificateSettings settings, X509Certificate2 certificate)
    {
        settings.CertificateLocation = TestCertificates.WritePfx(certificate, Password);
        settings.CertificatePassword = Password;
    }

    private static GlobalSettings.FutureCertificateSettings FutureSlot(X509Certificate2 certificate, bool active) => new()
    {
        CertificateLocation = TestCertificates.WritePfx(certificate, Password),
        CertificatePassword = Password,
        Enabled = true,
        Active = active,
    };

    private static string MissingLocation() =>
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx");

    private static GlobalSettings.FutureCertificateSettings MissingFutureSlot() => new()
    {
        CertificateLocation = MissingLocation(),
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
