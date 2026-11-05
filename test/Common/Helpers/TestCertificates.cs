using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Bit.Test.Common.Helpers;

public static class TestCertificates
{
    private static readonly Lazy<string> _directory = new(CreateDirectory);

    public static X509Certificate2 Create(string subject = "CN=Bitwarden Test")
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now.AddDays(-1), now.AddDays(365));
    }

    public static string WritePfx(X509Certificate2 certificate, string password)
    {
        var path = Path.Combine(_directory.Value, $"{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        return path;
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bw-test-certificates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
        return directory;
    }
}
