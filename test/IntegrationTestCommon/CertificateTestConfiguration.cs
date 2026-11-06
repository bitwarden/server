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
        ["globalSettings:identityServer:futureCertificate:enabled"] = "false",
        ["globalSettings:identityServer:futureCertificate:active"] = "false",
    };

    public static Dictionary<string, string?> SelfHosted(X509Certificate2 certificate,
        X509Certificate2 futureCertificate, bool futureActive, params X509Certificate2[] validationCertificates)
    {
        var configuration = SelfHosted(certificate);

        const string future = "globalSettings:identityServer:futureCertificate";
        AddCertificate(configuration, future, futureCertificate);
        configuration[$"{future}:active"] = futureActive.ToString();

        for (var i = 0; i < validationCertificates.Length; i++)
        {
            AddCertificate(configuration, $"globalSettings:identityServer:validationCertificates:{i}",
                validationCertificates[i]);
        }

        return configuration;
    }

    public static Dictionary<string, string?> NoCertificate() => new()
    {
        ["globalSettings:selfHosted"] = "false",
        ["globalSettings:identityServer:certificateLocation"] = "",
        ["globalSettings:identityServer:certificatePassword"] = "",
        ["globalSettings:identityServer:certificateThumbprint"] = "",
        ["globalSettings:identityServer:futureCertificate:enabled"] = "false",
        ["globalSettings:identityServer:futureCertificate:active"] = "false",
    };

    private static void AddCertificate(Dictionary<string, string?> configuration, string path,
        X509Certificate2 certificate)
    {
        configuration[$"{path}:certificateLocation"] = TestCertificates.WritePfx(certificate, CertificatePassword);
        configuration[$"{path}:certificatePassword"] = CertificatePassword;
        configuration[$"{path}:certificateThumbprint"] = "";
        configuration[$"{path}:blobName"] = "";
        configuration[$"{path}:enabled"] = "true";
    }
}
