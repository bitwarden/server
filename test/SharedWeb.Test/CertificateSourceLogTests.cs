using System.Security.Cryptography.X509Certificates;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Bit.SharedWeb.Test;

public class CertificateSourceLogTests
{
    private const string Password = "Distinctive-Password-7f3c";

    [Theory]
    [InlineData(CertificateRole.IdentitySigning, "IdentityServer")]
    [InlineData(CertificateRole.SsoSigning, "SsoServer")]
    [InlineData(CertificateRole.SsoEncryption, "SsoServer")]
    public void LogCertificateSources_LogsRoleSourceAndEachThumbprint(CertificateRole role, string source)
    {
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");
        using var retiredA = TestCertificates.Create("CN=Bitwarden Test Retired A");
        using var retiredB = TestCertificates.Create("CN=Bitwarden Test Retired B");
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger,
            [new CertificateSourceReport(role, active, [staged], [retiredA, retiredB], IsFallback: false)],
            new GlobalSettings());

        var entry = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(Constants.BypassFiltersEventId, entry.Id.Id);
        Assert.Equal(role.GetDisplayName(), Value(entry, "Role"));
        Assert.Equal(source, Value(entry, "Source"));
        Assert.Equal(active.Thumbprint, Value(entry, "ActiveThumbprint"));
        Assert.Equal(staged.Thumbprint, Value(entry, "StagedThumbprints"));
        Assert.Equal($"{retiredA.Thumbprint}, {retiredB.Thumbprint}", Value(entry, "RetiredThumbprints"));
        Assert.Contains($"Staged thumbprints: {staged.Thumbprint}.", entry.Message);
        Assert.Contains(
            $"Retired (validation or decryption) thumbprints: {retiredA.Thumbprint}, {retiredB.Thumbprint}.",
            entry.Message);
    }

    [Theory]
    [InlineData(CertificateRole.SsoSigning)]
    [InlineData(CertificateRole.SsoEncryption)]
    public void LogCertificateSources_Fallback_LogsIdentityFallbackSource(CertificateRole role)
    {
        using var active = TestCertificates.Create();
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger,
            [new CertificateSourceReport(role, active, [], [], IsFallback: true)], new GlobalSettings());

        var entry = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Contains("source: Identity fallback.", entry.Message);
        Assert.Contains(active.Thumbprint, entry.Message);
    }

    [Fact]
    public void LogCertificateSources_NoActiveCertificate_LogsNone()
    {
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger,
            [new CertificateSourceReport(CertificateRole.IdentitySigning, null, [], [], IsFallback: false)],
            new GlobalSettings());

        var entry = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Active thumbprint: none.", entry.Message);
        Assert.Contains("Staged thumbprints: none.", entry.Message);
        Assert.Contains("thumbprints: none.", entry.Message.Split("Retired")[1]);
    }

    [Fact]
    public void LogCertificateSources_OneEntryForEachRole()
    {
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger,
            [
                new CertificateSourceReport(CertificateRole.SsoSigning, null, [], [], IsFallback: false),
                new CertificateSourceReport(CertificateRole.SsoEncryption, null, [], [], IsFallback: false),
            ],
            new GlobalSettings());

        var entries = logger.Collector.GetSnapshot();
        Assert.Equal(2, entries.Count);
        Assert.Contains("SSO signing", entries[0].Message);
        Assert.Contains("SSO encryption", entries[1].Message);
    }

    [Theory]
    [InlineData(CertificateRole.IdentitySigning, "globalSettings:identityServer")]
    [InlineData(CertificateRole.SsoSigning, "globalSettings:ssoServer")]
    [InlineData(CertificateRole.SsoEncryption, "globalSettings:ssoServer:encryption")]
    public void LogCertificateSources_PasswordWithNoLocationThumbprintOrBlobName_LogsWarning(CertificateRole role,
        string settingPath)
    {
        var globalSettings = CleanSettings();
        var root = Root(globalSettings, role);
        root.CertificatePassword = Password;
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger, [Report(role)], globalSettings);

        var warning = Assert.Single(logger.Collector.GetSnapshot(), entry => entry.Level == LogLevel.Warning);
        Assert.Contains(settingPath, warning.Message);
        Assert.Contains("password", warning.Message);
    }

    [Theory]
    [InlineData(CertificateRole.IdentitySigning, "globalSettings:identityServer")]
    [InlineData(CertificateRole.SsoSigning, "globalSettings:ssoServer")]
    [InlineData(CertificateRole.SsoEncryption, "globalSettings:ssoServer:encryption")]
    public void LogCertificateSources_ActiveButNotEnabled_LogsWarning(CertificateRole role, string settingPath)
    {
        var globalSettings = CleanSettings();
        var future = Future(globalSettings, role);
        future.Active = true;
        future.Enabled = false;
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger, [Report(role)], globalSettings);

        var warning = Assert.Single(logger.Collector.GetSnapshot(), entry => entry.Level == LogLevel.Warning);
        Assert.Contains($"{settingPath}:futureCertificate", warning.Message);
    }

    [Theory]
    [InlineData(CertificateRole.IdentitySigning, "location")]
    [InlineData(CertificateRole.IdentitySigning, "thumbprint")]
    [InlineData(CertificateRole.IdentitySigning, "blobName")]
    [InlineData(CertificateRole.SsoSigning, "location")]
    [InlineData(CertificateRole.SsoSigning, "thumbprint")]
    [InlineData(CertificateRole.SsoSigning, "blobName")]
    [InlineData(CertificateRole.SsoEncryption, "location")]
    [InlineData(CertificateRole.SsoEncryption, "thumbprint")]
    [InlineData(CertificateRole.SsoEncryption, "blobName")]
    public void LogCertificateSources_CleanConfiguration_LogsNoWarning(CertificateRole role, string rootSource)
    {
        var globalSettings = CleanSettings();
        var root = Root(globalSettings, role);
        root.CertificatePassword = Password;
        switch (rootSource)
        {
            case "location":
                root.CertificateLocation = "/certificates/role.pfx";
                break;
            case "thumbprint":
                root.CertificateThumbprint = "0123456789ABCDEF0123456789ABCDEF01234567";
                break;
            default:
                root.BlobName = "role.pfx";
                break;
        }
        var future = Future(globalSettings, role);
        future.Enabled = true;
        future.Active = true;
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger, [Report(role)], globalSettings);

        Assert.DoesNotContain(logger.Collector.GetSnapshot(), entry => entry.Level != LogLevel.Information);
    }

    [Fact]
    public void LogCertificateSources_OnlyChecksTheSettingsOfTheReportedRoles()
    {
        var globalSettings = CleanSettings();
        globalSettings.SsoServer.CertificatePassword = Password;
        globalSettings.SsoServer.Encryption.FutureCertificate.Active = true;
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger, [Report(CertificateRole.IdentitySigning)],
            globalSettings);

        Assert.DoesNotContain(logger.Collector.GetSnapshot(), entry => entry.Level != LogLevel.Information);
    }

    [Fact]
    public void LogCertificateSources_NeverLogsAPasswordBlobNamePathOrSubject()
    {
        const string blobName = "distinctive-blob-name-9a1d.pfx";
        const string location = "/distinctive/location-4be2.pfx";
        const string subject = "Distinctive Subject 52c0";
        using var certificate = TestCertificates.Create($"CN={subject}");
        var globalSettings = CleanSettings();
        globalSettings.IdentityServer.CertificatePassword = Password;
        foreach (var role in new[] { CertificateRole.SsoSigning, CertificateRole.SsoEncryption })
        {
            var future = Future(globalSettings, role);
            future.CertificateLocation = location;
            future.BlobName = blobName;
            future.CertificatePassword = Password;
            future.Active = true;
        }
        globalSettings.SsoServer.CertificateLocation = location;
        globalSettings.SsoServer.BlobName = blobName;
        globalSettings.SsoServer.CertificatePassword = Password;
        var logger = new FakeLogger();

        ServiceCollectionExtensions.LogCertificateSources(logger,
            [
                new CertificateSourceReport(CertificateRole.IdentitySigning, certificate, [certificate],
                    [certificate], IsFallback: false),
                new CertificateSourceReport(CertificateRole.SsoSigning, certificate, [], [], IsFallback: false),
                new CertificateSourceReport(CertificateRole.SsoEncryption, certificate, [], [],
                    IsFallback: true),
            ],
            globalSettings);

        var entries = logger.Collector.GetSnapshot();
        Assert.Equal(3, entries.Count(entry => entry.Level == LogLevel.Warning));
        foreach (var entry in entries)
        {
            var texts = (entry.StructuredState ?? []).Select(pair => pair.Value ?? string.Empty)
                .Append(entry.Message);
            foreach (var text in texts)
            {
                Assert.DoesNotContain(Password, text);
                Assert.DoesNotContain(blobName, text);
                Assert.DoesNotContain(location, text);
                Assert.DoesNotContain(subject, text);
            }
        }
    }

    private static string? Value(FakeLogRecord entry, string key) =>
        Assert.Single(entry.StructuredState ?? [], pair => pair.Key == key).Value;

    private static CertificateSourceReport Report(CertificateRole role) =>
        new(role, null, Array.Empty<X509Certificate2>(), Array.Empty<X509Certificate2>(), IsFallback: false);

    private static GlobalSettings CleanSettings() => new()
    {
        IdentityServer =
        {
            CertificateLocation = null,
            CertificatePassword = null,
            CertificateThumbprint = null,
            BlobName = null,
        },
    };

    private static ICertificateSettings Root(GlobalSettings globalSettings, CertificateRole role) => role switch
    {
        CertificateRole.IdentitySigning => globalSettings.IdentityServer,
        CertificateRole.SsoSigning => globalSettings.SsoServer,
        _ => globalSettings.SsoServer.Encryption,
    };

    private static GlobalSettings.FutureCertificateSettings Future(GlobalSettings globalSettings, CertificateRole role) =>
        role switch
        {
            CertificateRole.IdentitySigning => globalSettings.IdentityServer.FutureCertificate,
            CertificateRole.SsoSigning => globalSettings.SsoServer.FutureCertificate,
            _ => globalSettings.SsoServer.Encryption.FutureCertificate,
        };
}
