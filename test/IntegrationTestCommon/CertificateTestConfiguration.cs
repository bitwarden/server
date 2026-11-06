using System.Security.Cryptography.X509Certificates;
using Bit.Test.Common.Helpers;

namespace Bit.IntegrationTestCommon;

public static class CertificateTestConfiguration
{
    public const string CertificatePassword = "test-password";

    private const string SsoServer = "globalSettings:ssoServer";

    public static Dictionary<string, string?> SelfHosted(X509Certificate2 certificate) => WithNoSsoServer(new()
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
    });

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

    public static Dictionary<string, string?> NoCertificate() => WithNoSsoServer(new()
    {
        ["globalSettings:selfHosted"] = "false",
        ["globalSettings:identityServer:certificateLocation"] = "",
        ["globalSettings:identityServer:certificatePassword"] = "",
        ["globalSettings:identityServer:certificateThumbprint"] = "",
        ["globalSettings:identityServer:futureCertificate:enabled"] = "false",
        ["globalSettings:identityServer:futureCertificate:active"] = "false",
    });

    /// <summary>
    /// Sets <paramref name="certificate"/> as the SSO signing certificate, from a self-hosted file.
    /// </summary>
    public static Dictionary<string, string?> WithSsoSigningCertificate(
        this Dictionary<string, string?> configuration, X509Certificate2 certificate)
    {
        configuration[$"{SsoServer}:certificateLocation"] = TestCertificates.WritePfx(certificate, CertificatePassword);
        configuration[$"{SsoServer}:certificatePassword"] = CertificatePassword;
        return configuration;
    }

    private static Dictionary<string, string?> WithNoSsoServer(Dictionary<string, string?> configuration)
    {
        foreach (var root in new[] { SsoServer, $"{SsoServer}:encryption" })
        {
            configuration[$"{root}:certificateLocation"] = "";
            configuration[$"{root}:certificateThumbprint"] = "";
            configuration[$"{root}:certificatePassword"] = "";
            configuration[$"{root}:blobName"] = "";
            configuration[$"{root}:futureCertificate:enabled"] = "false";
            configuration[$"{root}:futureCertificate:active"] = "false";
        }
        return configuration;
    }

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
