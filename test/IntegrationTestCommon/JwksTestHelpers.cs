using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Bit.IntegrationTestCommon;

public static class JwksTestHelpers
{
    private static readonly string[] _privateKeyParameters = ["d", "p", "q", "dp", "dq", "qi"];

    public static async Task<JsonElement[]> GetPublishedKeysAsync(HttpClient client)
    {
        using var discoveryResponse = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, discoveryResponse.StatusCode);
        using var discovery = JsonDocument.Parse(await discoveryResponse.Content.ReadAsStringAsync());
        var jwksPath = new Uri(discovery.RootElement.GetProperty("jwks_uri").GetString()!).LocalPath;

        using var jwksResponse = await client.GetAsync(jwksPath);
        Assert.Equal(HttpStatusCode.OK, jwksResponse.StatusCode);
        using var jwks = JsonDocument.Parse(await jwksResponse.Content.ReadAsStringAsync());
        return jwks.RootElement.GetProperty("keys").EnumerateArray().Select(k => k.Clone()).ToArray();
    }

    public static async Task AssertPublishesExactlyAsync(HttpClient client, X509Certificate2 certificate)
    {
        var key = Assert.Single(await GetPublishedKeysAsync(client));

        AssertIsPublicSigningKey(key, certificate);
    }

    public static async Task AssertPublishesExactlyKeyIdsAsync(HttpClient client,
        params X509Certificate2[] certificates)
    {
        var keys = await GetPublishedKeysAsync(client);

        Assert.Equivalent(certificates.Select(KeyId), keys.Select(key => key.GetProperty("kid").GetString()),
            strict: true);
        foreach (var certificate in certificates)
        {
            AssertIsPublicSigningKey(
                keys.Single(key => key.GetProperty("kid").GetString() == KeyId(certificate)), certificate);
        }
    }

    public static async Task AssertSignedByPublishedKeyAsync(HttpClient client, string token, X509Certificate2 certificate)
    {
        Assert.Equal(KeyId(certificate), new JsonWebToken(token).Kid);

        var key = Assert.Single(await GetPublishedKeysAsync(client));
        await AssertValidSignatureAsync(token, key);
    }

    public static async Task AssertSignedByOnePublishedKeyAsync(HttpClient client, string token,
        X509Certificate2 certificate)
    {
        Assert.Equal(KeyId(certificate), new JsonWebToken(token).Kid);

        var key = Assert.Single(await GetPublishedKeysAsync(client),
            published => published.GetProperty("kid").GetString() == KeyId(certificate));
        await AssertValidSignatureAsync(token, key);
    }

    public static void AssertIsOneDeveloperKey(JsonElement[] keys)
    {
        var key = Assert.Single(keys);

        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.DoesNotContain("RS256", key.GetProperty("kid").GetString());
        Assert.False(key.TryGetProperty("x5t", out _));
        Assert.False(key.TryGetProperty("x5c", out _));
    }

    private static void AssertIsPublicSigningKey(JsonElement key, X509Certificate2 certificate)
    {
        var parameters = certificate.GetRSAPublicKey()!.ExportParameters(false);
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.Equal(KeyId(certificate), key.GetProperty("kid").GetString());
        Assert.Equal(Base64UrlEncoder.Encode(certificate.GetCertHash()), key.GetProperty("x5t").GetString());
        Assert.Equal(Base64UrlEncoder.Encode(parameters.Modulus), key.GetProperty("n").GetString());
        Assert.Equal(Base64UrlEncoder.Encode(parameters.Exponent), key.GetProperty("e").GetString());
        Assert.Equal(Convert.ToBase64String(certificate.RawData), key.GetProperty("x5c")[0].GetString());
        foreach (var privateParameter in _privateKeyParameters)
        {
            Assert.False(key.TryGetProperty(privateParameter, out _),
                $"The published key has the private parameter {privateParameter}.");
        }
    }

    private static string KeyId(X509Certificate2 certificate) => certificate.Thumbprint + "RS256";

    private static async Task AssertValidSignatureAsync(string token, JsonElement key)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = new JsonWebKey(key.GetRawText()),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
        });
        Assert.True(result.IsValid, result.Exception?.Message);
    }
}
