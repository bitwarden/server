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

    [Fact]
    public void SsoServer_CertificateFieldsDefaultToNull()
    {
        var ssoServer = new GlobalSettings().SsoServer;

        Assert.Null(ssoServer.CertificateLocation);
        Assert.Null(ssoServer.CertificateThumbprint);
        Assert.Null(ssoServer.CertificatePassword);
        Assert.Null(ssoServer.BlobName);
        Assert.Null(ssoServer.Encryption.CertificateLocation);
        Assert.Null(ssoServer.Encryption.CertificateThumbprint);
        Assert.Null(ssoServer.Encryption.CertificatePassword);
        Assert.Null(ssoServer.Encryption.BlobName);
    }

    [Fact]
    public void SsoServer_Defaults()
    {
        var ssoServer = new GlobalSettings().SsoServer;

        Assert.False(ssoServer.FutureCertificate.Enabled);
        Assert.False(ssoServer.FutureCertificate.Active);
        Assert.Empty(ssoServer.ValidationCertificates);
        Assert.NotNull(ssoServer.Encryption);
        Assert.False(ssoServer.Encryption.FutureCertificate.Enabled);
        Assert.False(ssoServer.Encryption.FutureCertificate.Active);
        Assert.Empty(ssoServer.Encryption.DecryptionCertificates);
    }

    [Fact]
    public void SsoServer_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["globalSettings:ssoServer:certificateLocation"] = "/etc/bitwarden/sso/signing.pfx",
                ["globalSettings:ssoServer:certificateThumbprint"] = "SIGNING-THUMBPRINT",
                ["globalSettings:ssoServer:certificatePassword"] = "signing-password",
                ["globalSettings:ssoServer:blobName"] = "sso-signing.pfx",
                ["globalSettings:ssoServer:futureCertificate:certificateLocation"] = "/etc/bitwarden/sso/signing-future.pfx",
                ["globalSettings:ssoServer:futureCertificate:certificateThumbprint"] = "SIGNING-FUTURE-THUMBPRINT",
                ["globalSettings:ssoServer:futureCertificate:certificatePassword"] = "signing-future-password",
                ["globalSettings:ssoServer:futureCertificate:blobName"] = "sso-signing-future.pfx",
                ["globalSettings:ssoServer:futureCertificate:enabled"] = "true",
                ["globalSettings:ssoServer:futureCertificate:active"] = "true",
                ["globalSettings:ssoServer:validationCertificates:0:certificateLocation"] = "/etc/bitwarden/sso/signing-retired.pfx",
                ["globalSettings:ssoServer:validationCertificates:0:certificateThumbprint"] = "SIGNING-RETIRED-THUMBPRINT",
                ["globalSettings:ssoServer:validationCertificates:0:certificatePassword"] = "signing-retired-password",
                ["globalSettings:ssoServer:validationCertificates:0:blobName"] = "sso-signing-retired.pfx",
                ["globalSettings:ssoServer:validationCertificates:0:enabled"] = "false",
                ["globalSettings:ssoServer:encryption:certificateLocation"] = "/etc/bitwarden/sso/encryption.pfx",
                ["globalSettings:ssoServer:encryption:certificateThumbprint"] = "ENCRYPTION-THUMBPRINT",
                ["globalSettings:ssoServer:encryption:certificatePassword"] = "encryption-password",
                ["globalSettings:ssoServer:encryption:blobName"] = "sso-encryption.pfx",
                ["globalSettings:ssoServer:encryption:futureCertificate:certificateLocation"] = "/etc/bitwarden/sso/encryption-future.pfx",
                ["globalSettings:ssoServer:encryption:futureCertificate:certificateThumbprint"] = "ENCRYPTION-FUTURE-THUMBPRINT",
                ["globalSettings:ssoServer:encryption:futureCertificate:certificatePassword"] = "encryption-future-password",
                ["globalSettings:ssoServer:encryption:futureCertificate:blobName"] = "sso-encryption-future.pfx",
                ["globalSettings:ssoServer:encryption:futureCertificate:enabled"] = "true",
                ["globalSettings:ssoServer:encryption:futureCertificate:active"] = "true",
                ["globalSettings:ssoServer:encryption:decryptionCertificates:0:certificateLocation"] = "/etc/bitwarden/sso/encryption-retired.pfx",
                ["globalSettings:ssoServer:encryption:decryptionCertificates:0:certificateThumbprint"] = "ENCRYPTION-RETIRED-THUMBPRINT",
                ["globalSettings:ssoServer:encryption:decryptionCertificates:0:certificatePassword"] = "encryption-retired-password",
                ["globalSettings:ssoServer:encryption:decryptionCertificates:0:blobName"] = "sso-encryption-retired.pfx",
                ["globalSettings:ssoServer:encryption:decryptionCertificates:0:enabled"] = "false",
            })
            .Build();
        var globalSettings = new GlobalSettings();

        ConfigurationBinder.Bind(configuration.GetSection("GlobalSettings"), globalSettings);

        var signing = globalSettings.SsoServer;
        Assert.Equal("/etc/bitwarden/sso/signing.pfx", signing.CertificateLocation);
        Assert.Equal("SIGNING-THUMBPRINT", signing.CertificateThumbprint);
        Assert.Equal("signing-password", signing.CertificatePassword);
        Assert.Equal("sso-signing.pfx", signing.BlobName);

        var signingFuture = signing.FutureCertificate;
        Assert.Equal("/etc/bitwarden/sso/signing-future.pfx", signingFuture.CertificateLocation);
        Assert.Equal("SIGNING-FUTURE-THUMBPRINT", signingFuture.CertificateThumbprint);
        Assert.Equal("signing-future-password", signingFuture.CertificatePassword);
        Assert.Equal("sso-signing-future.pfx", signingFuture.BlobName);
        Assert.True(signingFuture.Enabled);
        Assert.True(signingFuture.Active);

        var signingRetired = Assert.Single(signing.ValidationCertificates);
        Assert.Equal("/etc/bitwarden/sso/signing-retired.pfx", signingRetired.CertificateLocation);
        Assert.Equal("SIGNING-RETIRED-THUMBPRINT", signingRetired.CertificateThumbprint);
        Assert.Equal("signing-retired-password", signingRetired.CertificatePassword);
        Assert.Equal("sso-signing-retired.pfx", signingRetired.BlobName);
        Assert.False(signingRetired.Enabled);

        var encryption = signing.Encryption;
        Assert.Equal("/etc/bitwarden/sso/encryption.pfx", encryption.CertificateLocation);
        Assert.Equal("ENCRYPTION-THUMBPRINT", encryption.CertificateThumbprint);
        Assert.Equal("encryption-password", encryption.CertificatePassword);
        Assert.Equal("sso-encryption.pfx", encryption.BlobName);

        var encryptionFuture = encryption.FutureCertificate;
        Assert.Equal("/etc/bitwarden/sso/encryption-future.pfx", encryptionFuture.CertificateLocation);
        Assert.Equal("ENCRYPTION-FUTURE-THUMBPRINT", encryptionFuture.CertificateThumbprint);
        Assert.Equal("encryption-future-password", encryptionFuture.CertificatePassword);
        Assert.Equal("sso-encryption-future.pfx", encryptionFuture.BlobName);
        Assert.True(encryptionFuture.Enabled);
        Assert.True(encryptionFuture.Active);

        var encryptionRetired = Assert.Single(encryption.DecryptionCertificates);
        Assert.Equal("/etc/bitwarden/sso/encryption-retired.pfx", encryptionRetired.CertificateLocation);
        Assert.Equal("ENCRYPTION-RETIRED-THUMBPRINT", encryptionRetired.CertificateThumbprint);
        Assert.Equal("encryption-retired-password", encryptionRetired.CertificatePassword);
        Assert.Equal("sso-encryption-retired.pfx", encryptionRetired.BlobName);
        Assert.False(encryptionRetired.Enabled);
    }
}
