using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Test.Common.Helpers;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Sustainsys.Saml2;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

public class SsoCertificateTests
{
    private const string OidcIdentityClientKey = "test-oidc-identity-client-key";
    private static readonly XNamespace _md = "urn:oasis:names:tc:SAML:2.0:metadata";
    private static readonly XNamespace _ds = "http://www.w3.org/2000/09/xmldsig#";

    [Fact]
    public async Task Jwks_WithOneIdentityCertificate_PublishesExactlyThatKey()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            factory.UpdateConfiguration(key, value);
        }
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyAsync(client, certificate);
    }

    [Fact]
    public async Task Metadata_WithOneIdentityCertificate_HasOneKeyDescriptorWithNoUse()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.AsSelfHosted().BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var keyDescriptor = Assert.Single(XDocument.Parse(xml).Descendants(_md + "KeyDescriptor"));
        Assert.Null(keyDescriptor.Attribute("use"));
        Assert.Equal(
            Convert.ToBase64String(certificate.RawData),
            keyDescriptor.Descendants(_ds + "X509Certificate").Single().Value.Trim());
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToTheIdentityCertificate_Decrypts()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLogin.BuildSignedAssertion(login, idpCertificate), certificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await Saml2SpInitiatedLogin.PostAcsAsync(arrangement, login, responseXml);

        // Assert
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Metadata_WithOneIdentityCertificate_IsSignedWithThatCertificate()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.AsSelfHosted().BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(IsSignedBy(XmlHelpers.XmlDocumentFromString(xml).DocumentElement!, certificate));
    }

    [Fact]
    public async Task AuthnRequest_WithSigningAlwaysAndOneIdentityCertificate_IsSignedWithThatCertificate()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, selfHosted: true, spSigningBehavior: Saml2SigningBehavior.Always,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));

        // Act
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);

        // Assert
        Assert.True(IsSignedBy(login.AuthnRequest, certificate));
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToAnotherCertificate_Fails()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var otherCertificate = TestCertificates.Create("CN=Another Certificate");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLogin.BuildSignedAssertion(login, idpCertificate), otherCertificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await Saml2SpInitiatedLogin.PostAcsAsync(arrangement, login, responseXml);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Encrypted Assertion(s) could not be decrypted using the configured Service Certificate(s).",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Jwks_DevelopmentWithNoCertificate_PublishesOneDeveloperKey()
    {
        // Arrange
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            factory.UpdateConfiguration(key, value);
        }
        using var client = factory.CreateClient();

        // Act
        var keys = await JwksTestHelpers.GetPublishedKeysAsync(client);

        // Assert
        JwksTestHelpers.AssertIsOneDeveloperKey(keys);
    }

    [Fact]
    public async Task Metadata_DevelopmentWithNoCertificate_HasNoKeyDescriptor()
    {
        // Arrange
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = XDocument.Parse(xml);
        Assert.Single(document.Descendants(_md + "SPSSODescriptor"));
        Assert.Empty(document.Descendants(_md + "KeyDescriptor"));
    }

    [Fact]
    public async Task Jwks_WithSsoSigningCertificate_DiffersFromIdentity()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var ssoCertificate = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate)
                     .WithSsoSigningCertificate(ssoCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyAsync(client, ssoCertificate);
    }

    [Fact]
    public void Startup_ProductionWithNoSsoServer_Fails()
    {
        // Arrange
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act & Assert
        StartupTestHelpers.AssertStartupFails(factory, "Production", "globalSettings:ssoServer");
    }

    [Fact]
    public async Task Token_WithSsoSigningCertificate_IsSignedByThePublishedSsoKey()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var ssoCertificate = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate)
                     .WithSsoSigningCertificate(ssoCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }
        factory.UpdateConfiguration("globalSettings:oidcIdentityClientKey", OidcIdentityClientKey);
        using var client = factory.CreateClient();
        var redirectUri = $"{factory.GetService<GlobalSettings>().BaseServiceUri.Identity}/signin-oidc";
        var codeVerifier = CryptoRandom.CreateUniqueId(32);
        var code = await StoreOidcIdentityAuthorizationCodeAsync(factory, redirectUri, codeVerifier);

        // Act
        using var response = await client.PostAsync(await GetTokenEndpointPathAsync(client),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OidcConstants.GrantTypes.AuthorizationCode,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
                ["client_id"] = "oidc-identity",
                ["client_secret"] = OidcIdentityClientKey,
            }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await JwksTestHelpers.AssertSignedByPublishedKeyAsync(client,
            tokens.RootElement.GetProperty("id_token").GetString()!, ssoCertificate);
    }

    private static async Task<string> StoreOidcIdentityAuthorizationCodeAsync(SsoApplicationFactory factory,
        string redirectUri, string codeVerifier)
    {
        var authorizationCode = new AuthorizationCode
        {
            ClientId = "oidc-identity",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = redirectUri,
            RequestedScopes = [OidcConstants.StandardScopes.OpenId],
            IsOpenId = true,
            CodeChallenge = codeVerifier.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Plain,
            Subject = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(JwtClaimTypes.Subject, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.IdentityProvider, "local"),
                new Claim(JwtClaimTypes.AuthenticationTime,
                    DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            ], "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role)),
        };

        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthorizationCodeStore>()
            .StoreAuthorizationCodeAsync(authorizationCode);
    }

    private static async Task<string> GetTokenEndpointPathAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var discovery = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new Uri(discovery.RootElement.GetProperty("token_endpoint").GetString()!).LocalPath;
    }

    private static SsoConfigurationData BuildSamlConfiguration() => new()
    {
        ConfigType = SsoType.Saml2,
        IdpEntityId = "https://idp.example.com",
        IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
        IdpX509PublicCert = CoreHelpers.Base64UrlEncode(Saml2AcsPostHarness.BuildIdpCertificate().RawData),
    };

    private static bool IsSignedBy(XmlElement element, X509Certificate2 certificate)
    {
        var signature = Assert.Single(element.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)
            .Cast<XmlElement>());
        var signedXml = new SignedXml(element);
        signedXml.LoadXml(signature);
        return signedXml.CheckSignature(certificate, verifySignatureOnly: true);
    }
}
