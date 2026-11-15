using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Bit.Core.Settings;
using Bit.IntegrationTestCommon;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Test.Common.Helpers;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Xunit;

namespace Bit.Sso.IntegrationTest.Certificates;

public class SsoSigningKeyTests
{
    private const string OidcIdentityClientKey = "test-oidc-identity-client-key";

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
}
