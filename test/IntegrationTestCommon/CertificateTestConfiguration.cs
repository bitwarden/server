using System.Security.Cryptography.X509Certificates;
using Bit.Test.Common.Helpers;

namespace Bit.IntegrationTestCommon;

public static class CertificateTestConfiguration
{
    public const string CertificatePassword = "test-password";

    private const string SsoServer = "globalSettings:ssoServer";
    private const string SsoEncryption = $"{SsoServer}:encryption";

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

    /// <summary>
    /// Sets <paramref name="certificate"/> as the staged SSO signing certificate, from a self-hosted file.
    /// </summary>
    public static Dictionary<string, string?> WithSsoStagedSigningCertificate(
        this Dictionary<string, string?> configuration, X509Certificate2 certificate)
    {
        AddCertificate(configuration, $"{SsoServer}:futureCertificate", certificate);
        configuration[$"{SsoServer}:futureCertificate:active"] = "false";
        return configuration;
    }

    /// <summary>
    /// Sets <paramref name="certificate"/> as the SSO encryption certificate, from a self-hosted file.
    /// </summary>
    public static Dictionary<string, string?> WithSsoEncryptionCertificate(
        this Dictionary<string, string?> configuration, X509Certificate2 certificate)
    {
        SetCertificate(configuration, SsoEncryption, certificate);
        return configuration;
    }

    /// <summary>
    /// Sets <paramref name="certificate"/> in the SSO encryption future slot, from a self-hosted file, with
    /// <c>Enabled = true</c> and the given <paramref name="active"/> value.
    /// </summary>
    public static Dictionary<string, string?> WithSsoEncryptionFutureCertificate(
        this Dictionary<string, string?> configuration, X509Certificate2 certificate, bool active)
    {
        AddCertificate(configuration, $"{SsoEncryption}:futureCertificate", certificate);
        configuration[$"{SsoEncryption}:futureCertificate:active"] = active.ToString();
        return configuration;
    }

    /// <summary>
    /// Sets <paramref name="certificates"/> as the SSO decryption certificates, from self-hosted files.
    /// </summary>
    public static Dictionary<string, string?> WithSsoDecryptionCertificates(
        this Dictionary<string, string?> configuration, params X509Certificate2[] certificates)
    {
        for (var i = 0; i < certificates.Length; i++)
        {
            AddCertificate(configuration, $"{SsoEncryption}:decryptionCertificates:{i}", certificates[i]);
        }
        return configuration;
    }

    private static Dictionary<string, string?> WithNoSsoServer(Dictionary<string, string?> configuration)
    {
        foreach (var root in new[] { SsoServer, SsoEncryption })
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
        SetCertificate(configuration, path, certificate);
        configuration[$"{path}:enabled"] = "true";
    }

    private static void SetCertificate(Dictionary<string, string?> configuration, string path,
        X509Certificate2 certificate)
    {
        configuration[$"{path}:certificateLocation"] = TestCertificates.WritePfx(certificate, CertificatePassword);
        configuration[$"{path}:certificatePassword"] = CertificatePassword;
        configuration[$"{path}:certificateThumbprint"] = "";
        configuration[$"{path}:blobName"] = "";
    }
}
