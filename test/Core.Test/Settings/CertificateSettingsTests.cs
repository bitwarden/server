using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bit.Core.Test.Settings;

public class CertificateSettingsTests
{
    [Fact]
    public void IdentityServer_Defaults()
    {
        var identityServer = new GlobalSettings().IdentityServer;

        Assert.Equal("identity.pfx", identityServer.BlobName);
        Assert.False(identityServer.FutureCertificate.Enabled);
        Assert.False(identityServer.FutureCertificate.Active);
        Assert.Empty(identityServer.ValidationCertificates);
    }

    [Fact]
    public void RetiredCertificate_EnabledDefaultsToTrue()
    {
        Assert.True(new GlobalSettings.RetiredCertificateSettings().Enabled);
    }

    [Fact]
    public void IdentityServer_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["globalSettings:identityServer:blobName"] = "identity-root.pfx",
                ["globalSettings:identityServer:futureCertificate:certificateLocation"] = "/etc/bitwarden/identity/future.pfx",
                ["globalSettings:identityServer:futureCertificate:certificateThumbprint"] = "FUTURE-THUMBPRINT",
                ["globalSettings:identityServer:futureCertificate:certificatePassword"] = "future-password",
                ["globalSettings:identityServer:futureCertificate:blobName"] = "identity-future.pfx",
                ["globalSettings:identityServer:futureCertificate:enabled"] = "true",
                ["globalSettings:identityServer:futureCertificate:active"] = "true",
                ["globalSettings:identityServer:validationCertificates:0:certificateLocation"] = "/etc/bitwarden/identity/retired.pfx",
                ["globalSettings:identityServer:validationCertificates:0:certificateThumbprint"] = "RETIRED-THUMBPRINT",
                ["globalSettings:identityServer:validationCertificates:0:certificatePassword"] = "retired-password",
                ["globalSettings:identityServer:validationCertificates:0:blobName"] = "identity-retired.pfx",
                ["globalSettings:identityServer:validationCertificates:0:enabled"] = "false",
            })
            .Build();
        var globalSettings = new GlobalSettings();

        ConfigurationBinder.Bind(configuration.GetSection("GlobalSettings"), globalSettings);

        var identityServer = globalSettings.IdentityServer;
        Assert.Equal("identity-root.pfx", identityServer.BlobName);

        var future = identityServer.FutureCertificate;
        Assert.Equal("/etc/bitwarden/identity/future.pfx", future.CertificateLocation);
        Assert.Equal("FUTURE-THUMBPRINT", future.CertificateThumbprint);
        Assert.Equal("future-password", future.CertificatePassword);
        Assert.Equal("identity-future.pfx", future.BlobName);
        Assert.True(future.Enabled);
        Assert.True(future.Active);

        var retired = Assert.Single(identityServer.ValidationCertificates);
        Assert.Equal("/etc/bitwarden/identity/retired.pfx", retired.CertificateLocation);
        Assert.Equal("RETIRED-THUMBPRINT", retired.CertificateThumbprint);
        Assert.Equal("retired-password", retired.CertificatePassword);
        Assert.Equal("identity-retired.pfx", retired.BlobName);
        Assert.False(retired.Enabled);
    }
}
