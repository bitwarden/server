using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Setup;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Setup.Test;

public class ProgramTests
{
    [Fact(Explicit = true)]
    public async Task Install_Works()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var installationId = $"{Guid.NewGuid()}";
            var testApp = Substitute.For<Application>();
            testApp.RootDirectory.Returns(tempDir.FullName);
            testApp
                .ReadInput(Arg.Any<string>())
                .Returns(c =>
                {
                    var prompt = c.Arg<string>();
                    return prompt switch
                    {
                        "Enter your installation id (get at https://bitwarden.com/host)" => installationId,
                        "Enter your installation key" => "test-key",
                        "Enter your region (US/EU) [US]" => "",
                        _ => throw new NotImplementedException($"Prompt not configured: {prompt}"),
                    };
                });
            testApp
                .ReadQuestion(Arg.Any<string>())
                .Returns(c =>
                {
                    var prompt = c.Arg<string>();
                    return prompt switch
                    {
                        "Do you have a SSL certificate to use?" => false,
                        "Do you want to generate a self-signed SSL certificate?" => true,
                        _ => throw new NotImplementedException(prompt),
                    };
                });

            var mockHandler = new MockHttpMessageHandler();

            mockHandler
                .Expect(HttpMethod.Get, $"https://api.bitwarden.com/installations/{installationId}")
                .Respond(HttpStatusCode.OK, JsonContent.Create(new { enabled = true, }));

            testApp
                .GetHttpClient()
                .Returns(mockHandler.ToHttpClient());

            Program.MainCore([
                "-install", "1",
                "-domain", "example.com",
                "-letsencrypt", "n",
                "-os", "lin",
                "-corev", "test-version-does-not-exist",
                "-webv", "test-version-does-not-exist",
                "-dbname", "test-db",
                "-keyconnectorv", "test-version-does-not-exist",
            ], testApp);

            // Assert SSL certificate details
            var baseDir = Path.Join(tempDir.FullName, "ssl", "self", "example.com");
            var certFile = new FileInfo(Path.Join(baseDir, "certificate.crt"));
            Assert.True(certFile.Exists);
            var cert = new X509Certificate2(certFile.FullName);

            var hundredYearsFromNow = DateTime.Now.AddDays(36500);

            Assert.InRange(cert.NotAfter, hundredYearsFromNow.AddMinutes(-1), hundredYearsFromNow.AddMinutes(1));

            Assert.Equal("sha256RSA", cert.SignatureAlgorithm.FriendlyName);

            var names = cert.SubjectName.EnumerateRelativeDistinguishedNames().ToList();

            Assert.Equal(6, names.Count);

            Assert.Contains(names, n => n.GetSingleElementType().FriendlyName == "C" && n.GetSingleElementValue() == "US");
            Assert.Contains(names, n => n.GetSingleElementType().FriendlyName == "S" && n.GetSingleElementValue() == "California");
            Assert.Contains(names, n => n.GetSingleElementType().FriendlyName == "L" && n.GetSingleElementValue() == "Santa Barbara");
            Assert.Contains(names, n => n.GetSingleElementType().FriendlyName == "O" && n.GetSingleElementValue() == "Bitwarden Inc.");
            Assert.Contains(names, n => n.GetSingleElementType().FriendlyName == "OU" && n.GetSingleElementValue() == "Bitwarden");
            Assert.Contains(names, n => n.GetSingleElementType().FriendlyName == "CN" && n.GetSingleElementValue() == "example.com");

            Assert.Equal(3, cert.Extensions.Count);
            var san = Assert.Single(cert.Extensions.OfType<X509SubjectAlternativeNameExtension>());
            var dns = Assert.Single(san.EnumerateDnsNames());
            Assert.Equal("example.com", dns);
            Assert.Empty(san.EnumerateIPAddresses());
            Assert.False(san.Critical);

            var basicConstraints = Assert.Single(cert.Extensions.OfType<X509BasicConstraintsExtension>());
            Assert.True(basicConstraints.CertificateAuthority);
            Assert.False(basicConstraints.HasPathLengthConstraint);
            Assert.Equal(0, basicConstraints.PathLengthConstraint);
            Assert.False(basicConstraints.Critical);

            var subjectKeyIdentifier = Assert.Single(cert.Extensions.OfType<X509SubjectKeyIdentifierExtension>());
            Assert.False(subjectKeyIdentifier.Critical);

            // Validate that the private key can be imported in PEM format
            using var rsa = RSA.Create();
            rsa.ImportFromPem(
                await File.ReadAllTextAsync(Path.Combine(baseDir, "private.key"), TestContext.Current.CancellationToken)
            );

            Assert.Equal(4096, rsa.KeySize);
            Assert.Equal("RSA", rsa.KeyExchangeAlgorithm);

            // Assert other things
        }
        finally
        {
            tempDir.Delete(true);
        }
    }
}
