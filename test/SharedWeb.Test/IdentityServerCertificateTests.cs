using System.Security.Cryptography.X509Certificates;
using Azure.Storage.Blobs;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Bit.Test.Common.Helpers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bit.SharedWeb.Test;

public class IdentityServerCertificateTests : IClassFixture<IdentityServerCertificateTests.AzuriteFixture>
{
    private const string Password = "Test-Certificate-Password1";

    private readonly AzuriteFixture _azurite;

    public IdentityServerCertificateTests(AzuriteFixture azurite)
    {
        _azurite = azurite;
    }

    [Fact]
    public async Task Cloud_WithStorageAndPassword_LoadsIdentityPfxBlob()
    {
        using var expected = TestCertificates.Create();
        await _azurite.ResetAsync(expected, Password);

        var globalSettings = CloudSettings(Password, null);

        using var actual = CoreHelpers.GetIdentityServerCertificate(globalSettings);

        Assert.NotNull(actual);
        Assert.Equal(expected.Thumbprint, actual.Thumbprint);
    }

    [Fact]
    public async Task Cloud_WithCustomBlobName_LoadsThatBlob()
    {
        using var expected = TestCertificates.Create();
        await _azurite.ResetAsync(expected, Password, blobName: "rotated.pfx");

        var globalSettings = CloudSettings(Password, null);
        globalSettings.IdentityServer.BlobName = "rotated.pfx";

        using var actual = CoreHelpers.GetIdentityServerCertificate(globalSettings);

        Assert.NotNull(actual);
        Assert.Equal(expected.Thumbprint, actual.Thumbprint);
    }

    [Fact]
    public async Task Cloud_WithCustomBlobNameNotInStorage_ReturnsNullWithoutUsingIdentityPfx()
    {
        using var certificate = TestCertificates.Create();
        await _azurite.ResetAsync(certificate, Password);

        var globalSettings = CloudSettings(Password, null);
        globalSettings.IdentityServer.BlobName = "rotated.pfx";

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public async Task SelfHosted_WithStorageAndPassword_IgnoresTheBlob()
    {
        using var certificate = TestCertificates.Create();
        await _azurite.ResetAsync(certificate, Password);

        var globalSettings = CloudSettings(Password, null);
        globalSettings.SelfHosted = true;
        globalSettings.IdentityServer.CertificateLocation =
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.pfx");

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public async Task Cloud_ThumbprintNotInStoreWithLoadableBlob_ReturnsNullWithoutUsingTheBlob()
    {
        using var certificate = TestCertificates.Create();
        using var unstored = TestCertificates.Create("CN=Bitwarden Test Unstored");
        await _azurite.ResetAsync(certificate, Password);

        var globalSettings = CloudSettings(Password, unstored.Thumbprint);

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public async Task Cloud_PlaceholderThumbprintWithLoadableBlob_LoadsTheBlob()
    {
        using var expected = TestCertificates.Create();
        await _azurite.ResetAsync(expected, Password);

        var globalSettings = CloudSettings(Password, "REPLACE");

        using var actual = CoreHelpers.GetIdentityServerCertificate(globalSettings);

        Assert.NotNull(actual);
        Assert.Equal(expected.Thumbprint, actual.Thumbprint);
    }

    [Theory]
    [InlineData("WrongPassword")]
    [InlineData("MissingBlob")]
    [InlineData("MissingContainer")]
    public async Task Cloud_BlobCannotBeLoaded_ReturnsNull(string scenario)
    {
        using var certificate = TestCertificates.Create();
        switch (scenario)
        {
            case "WrongPassword":
                await _azurite.ResetAsync(certificate, "Another-Certificate-Password1");
                break;
            case "MissingBlob":
                await _azurite.ResetAsync(null, null);
                break;
            default:
                await _azurite.ResetAsync(null, null, createContainer: false);
                break;
        }

        var globalSettings = CloudSettings(Password, null);

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public async Task Cloud_PlaceholderPasswordWithLoadableBlob_ReturnsNull()
    {
        using var certificate = TestCertificates.Create();
        await _azurite.ResetAsync(certificate, "secret");

        var globalSettings = CloudSettings("secret", null);

        Assert.Null(CoreHelpers.GetIdentityServerCertificate(globalSettings));
    }

    [Fact]
    public async Task AddIdentityServerCertificate_WithCertificate_RegistersItAsTheSigningKey()
    {
        using var certificate = TestCertificates.Create();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = true,
            IdentityServer =
            {
                CertificateLocation = TestCertificates.WritePfx(certificate, Password),
                CertificatePassword = Password,
                CertificateThumbprint = null,
            },
        };

        using var provider = BuildProvider("Production", globalSettings);

        var credential = await provider.GetRequiredService<ISigningCredentialStore>().GetSigningCredentialsAsync();

        Assert.Equal(certificate.Thumbprint + "RS256", credential.Kid);
    }

    [Fact]
    public async Task AddIdentityServerCertificate_DevelopmentWithCertificate_RegistersItAsTheSigningKey()
    {
        using var certificate = TestCertificates.Create();
        var globalSettings = NoCertificateSettings();
        globalSettings.SelfHosted = true;
        globalSettings.IdentityServer.CertificateLocation = TestCertificates.WritePfx(certificate, Password);
        globalSettings.IdentityServer.CertificatePassword = Password;

        using var provider = BuildProvider("Development", globalSettings);

        var credential = await provider.GetRequiredService<ISigningCredentialStore>().GetSigningCredentialsAsync();

        Assert.Equal(certificate.Thumbprint + "RS256", credential.Kid);
    }

    [Fact]
    public async Task AddIdentityServerCertificate_DevelopmentWithNoCertificate_RegistersADeveloperKey()
    {
        var globalSettings = NoCertificateSettings();

        using var provider = BuildProvider("Development", globalSettings);

        var credential = await provider.GetRequiredService<ISigningCredentialStore>().GetSigningCredentialsAsync();

        Assert.NotNull(credential);
        Assert.False(string.IsNullOrEmpty(credential.Kid));
    }

    [Fact]
    public async Task AddIdentityServerCertificate_DevelopmentWithDirectory_PersistsTheSigningKey()
    {
        var directory = CreateTempDirectory();
        try
        {
            var globalSettings = NoCertificateSettings();
            globalSettings.DevelopmentDirectory = directory;

            using var provider = BuildProvider("Development", globalSettings);

            var credential = await provider.GetRequiredService<ISigningCredentialStore>().GetSigningCredentialsAsync();

            Assert.NotNull(credential);
            Assert.True(File.Exists(Path.Combine(directory, "signingkey.jwk")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddIdentityServerCertificate_NotDevelopmentWithDirectoryAndNoCertificate_ThrowsAndWritesNoKey(
        string environmentName)
    {
        var directory = CreateTempDirectory();
        try
        {
            var globalSettings = NoCertificateSettings();
            globalSettings.DevelopmentDirectory = directory;

            var exception = Assert.Throws<Exception>(() => BuildProvider(environmentName, globalSettings));

            Assert.Equal("No identity certificate to use.", exception.Message);
            Assert.False(File.Exists(Path.Combine(directory, "signingkey.jwk")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void AddIdentityServerCertificate_ProductionWithNoCertificate_Throws()
    {
        var globalSettings = NoCertificateSettings();

        var exception = Assert.Throws<Exception>(() => BuildProvider("Production", globalSettings));

        Assert.Equal("No identity certificate to use.", exception.Message);
    }

    private static GlobalSettings NoCertificateSettings()
    {
        var globalSettings = new GlobalSettings
        {
            SelfHosted = false,
            DevelopmentDirectory = null,
            IdentityServer =
            {
                CertificateLocation = "identity.pfx",
                CertificatePassword = null,
                CertificateThumbprint = null,
            },
        };
        globalSettings.Storage.ConnectionString = null;
        return globalSettings;
    }

    private static ServiceProvider BuildProvider(string environmentName, GlobalSettings globalSettings)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServer().AddIdentityServerCertificate(environment, globalSettings);
        return services.BuildServiceProvider();
    }

    private GlobalSettings CloudSettings(string password, string thumbprint)
    {
        var globalSettings = new GlobalSettings
        {
            SelfHosted = false,
            IdentityServer =
            {
                CertificateLocation = "identity.pfx",
                CertificatePassword = password,
                CertificateThumbprint = thumbprint,
            },
        };
        globalSettings.Storage.ConnectionString = _azurite.ConnectionString;
        return globalSettings;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bw-identity-development-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public sealed class AzuriteFixture : IAsyncLifetime
    {
        private const string AzuriteImage =
            "mcr.microsoft.com/azure-storage/azurite:3.37.0@sha256:830430c1da1a2d537e08f3e6764dd1f5ae00cf0346bcaf625b968ec3f0971fd5";

        private readonly IContainer _container = new ContainerBuilder(AzuriteImage)
            .WithPortBinding(10000, true)
            .Build();

        public string ConnectionString { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _container.StartAsync();
            ConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{_container.Hostname}:{_container.GetMappedPublicPort(10000)}/devstoreaccount1;";
        }

        public async Task DisposeAsync() => await _container.DisposeAsync();

        public async Task ResetAsync(X509Certificate2 certificate, string password, bool createContainer = true,
            string blobName = "identity.pfx")
        {
            var container = new BlobServiceClient(ConnectionString).GetBlobContainerClient("certificates");
            await container.DeleteIfExistsAsync();
            if (!createContainer)
            {
                return;
            }

            await container.CreateAsync();
            if (certificate != null)
            {
                await container.UploadBlobAsync(blobName,
                    new BinaryData(certificate.Export(X509ContentType.Pfx, password)));
            }
        }
    }
}
