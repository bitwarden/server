using System.Security.Cryptography.X509Certificates;
using Bit.Core;
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
    [InlineData(CertificateSourceReport.IdentitySigning, "IdentityServer")]
    [InlineData(CertificateSourceReport.SsoSigning, "SsoServer")]
    [InlineData(CertificateSourceReport.SsoEncryption, "SsoServer")]
    public void LogCertificateSources_LogsRoleSourceAndEachThumbprint(string role, string source)
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
        Assert.Equal(role, Value(entry, "Role"));
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
    [InlineData(CertificateSourceReport.SsoSigning)]
    [InlineData(CertificateSourceReport.SsoEncryption)]
    public void LogCertificateSources_Fallback_LogsIdentityFallbackSource(string role)
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
            [new CertificateSourceReport(CertificateSourceReport.IdentitySigning, null, [], [], IsFallback: false)],
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
                new CertificateSourceReport(CertificateSourceReport.SsoSigning, null, [], [], IsFallback: false),
                new CertificateSourceReport(CertificateSourceReport.SsoEncryption, null, [], [], IsFallback: false),
            ],
            new GlobalSettings());

        var entries = logger.Collector.GetSnapshot();
        Assert.Equal(2, entries.Count);
        Assert.Contains(CertificateSourceReport.SsoSigning, entries[0].Message);
        Assert.Contains(CertificateSourceReport.SsoEncryption, entries[1].Message);
    }

    [Theory]
    [InlineData(CertificateSourceReport.IdentitySigning, "globalSettings:identityServer")]
    [InlineData(CertificateSourceReport.SsoSigning, "globalSettings:ssoServer")]
    [InlineData(CertificateSourceReport.SsoEncryption, "globalSettings:ssoServer:encryption")]
    public void LogCertificateSources_PasswordWithNoLocationThumbprintOrBlobName_LogsWarning(string role,
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
    [InlineData(CertificateSourceReport.IdentitySigning, "globalSettings:identityServer")]
    [InlineData(CertificateSourceReport.SsoSigning, "globalSettings:ssoServer")]
    [InlineData(CertificateSourceReport.SsoEncryption, "globalSettings:ssoServer:encryption")]
    public void LogCertificateSources_ActiveButNotEnabled_LogsWarning(string role, string settingPath)
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
    [InlineData(CertificateSourceReport.IdentitySigning, "location")]
    [InlineData(CertificateSourceReport.IdentitySigning, "thumbprint")]
    [InlineData(CertificateSourceReport.IdentitySigning, "blobName")]
    [InlineData(CertificateSourceReport.SsoSigning, "location")]
    [InlineData(CertificateSourceReport.SsoSigning, "thumbprint")]
    [InlineData(CertificateSourceReport.SsoSigning, "blobName")]
    [InlineData(CertificateSourceReport.SsoEncryption, "location")]
    [InlineData(CertificateSourceReport.SsoEncryption, "thumbprint")]
    [InlineData(CertificateSourceReport.SsoEncryption, "blobName")]
    public void LogCertificateSources_CleanConfiguration_LogsNoWarning(string role, string rootSource)
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

        ServiceCollectionExtensions.LogCertificateSources(logger, [Report(CertificateSourceReport.IdentitySigning)],
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
        foreach (var role in new[] { CertificateSourceReport.SsoSigning, CertificateSourceReport.SsoEncryption })
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
                new CertificateSourceReport(CertificateSourceReport.IdentitySigning, certificate, [certificate],
                    [certificate], IsFallback: false),
                new CertificateSourceReport(CertificateSourceReport.SsoSigning, certificate, [], [], IsFallback: false),
                new CertificateSourceReport(CertificateSourceReport.SsoEncryption, certificate, [], [],
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

    private static CertificateSourceReport Report(string role) =>
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

    private static ICertificateSettings Root(GlobalSettings globalSettings, string role) => role switch
    {
        CertificateSourceReport.IdentitySigning => globalSettings.IdentityServer,
        CertificateSourceReport.SsoSigning => globalSettings.SsoServer,
        _ => globalSettings.SsoServer.Encryption,
    };

    private static GlobalSettings.FutureCertificateSettings Future(GlobalSettings globalSettings, string role) =>
        role switch
        {
            CertificateSourceReport.IdentitySigning => globalSettings.IdentityServer.FutureCertificate,
            CertificateSourceReport.SsoSigning => globalSettings.SsoServer.FutureCertificate,
            _ => globalSettings.SsoServer.Encryption.FutureCertificate,
        };
}
