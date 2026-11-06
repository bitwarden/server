using System.Security.Cryptography.X509Certificates;
using Bit.Test.Common.Helpers;

namespace Bit.IntegrationTestCommon;

public static class CertificateTestConfiguration
{
    public const string CertificatePassword = "test-password";

    public static Dictionary<string, string?> SelfHosted(X509Certificate2 certificate) => new()
    {
        ["globalSettings:selfHosted"] = "true",
        ["globalSettings:installation:id"] = Guid.NewGuid().ToString(),
        ["globalSettings:installation:key"] = "installation-key",
        ["globalSettings:dataProtection:directory"] = TestCertificates.CreateSubdirectory(),
        ["globalSettings:identityServer:certificateLocation"] = TestCertificates.WritePfx(certificate, CertificatePassword),
        ["globalSettings:identityServer:certificatePassword"] = CertificatePassword,
        ["globalSettings:identityServer:certificateThumbprint"] = "",
    };

    public static Dictionary<string, string?> NoCertificate() => new()
    {
        ["globalSettings:selfHosted"] = "false",
        ["globalSettings:identityServer:certificateLocation"] = "",
        ["globalSettings:identityServer:certificatePassword"] = "",
        ["globalSettings:identityServer:certificateThumbprint"] = "",
    };
}
