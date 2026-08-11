using System.Text;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.IntegrationTestCommon.Fido2;

namespace Bit.Api.IntegrationTest.Controllers.TwoFactor;

/// <summary>
/// Shared stateless helpers, JSON fixtures, and HTTP utilities used across the per-provider
/// <c>TwoFactorController</c> integration-test classes. State-bound wrappers
/// (<c>EnrollUserInDuo</c>, etc.) live in each test class and delegate here.
/// </summary>
internal static class TwoFactorIntegrationTestHelpers
{
    public const string MasterPasswordHash = "master_password_hash";
    public const string AuthenticatorKey = "JBSWY3DPEHPK3PXP";

    // ---------------------------------------------------------------------
    // Token minting
    // ---------------------------------------------------------------------

    public static string ProtectUserVerificationToken(
        IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> factory,
        User user,
        TwoFactorProviderType providerType) =>
        factory.Protect(new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = providerType,
            ExpirationDate = DateTime.UtcNow.AddMinutes(30),
        });

    // ---------------------------------------------------------------------
    // Provider JSON fixtures (mirror what the User entity's serializer stores)
    // ---------------------------------------------------------------------

    public static string BuildAuthenticatorProvidersJson(string authenticatorKey) =>
        $"{{\"0\":{{\"Enabled\":true,\"MetaData\":{{\"Key\":\"{authenticatorKey}\"}}}}}}";

    public static string BuildYubiKeyProvidersJson() =>
        "{\"3\":{\"Enabled\":true,\"MetaData\":{\"Key1\":\"ccccccccccbe\",\"Nfc\":true}}}";

    public static string BuildDuoProvidersJson() =>
        "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"" + new string('s', 40)
        + "\",\"ClientId\":\"" + new string('c', 20) + "\",\"Host\":\"api-test.duosecurity.com\"}}}";

    public static string BuildEmailProvidersJson(string email) =>
        "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"" + email + "\"}}}";

    public static string BuildWebAuthnProvidersJson(int credentialCount = 1)
    {
        // DeleteTwoFactorWebAuthnCredentialCommand refuses per-credential deletion when only one
        // credential remains, so tests that exercise per-credential DELETE against the real
        // command need at least two seeded credentials.
        var credentials = string.Join(",",
            Enumerable.Range(0, credentialCount).Select(i => BuildWebAuthnCredentialJson(i)));
        return "{\"7\":{\"Enabled\":true,\"MetaData\":{" + credentials + "}}}";
    }

    // Mirrors the shape JsonHelpers.LegacySerialize (Newtonsoft) actually persists: Descriptor.Id
    // is standard Base64 (Convert.ToBase64String), not Fido2NetLib's Base64Url. Lets callers pin a
    // specific Id, e.g. one containing '+'/'/' to exercise the Base64UrlConverter decode path.
    public static string BuildWebAuthnProvidersJsonWithDescriptorId(string descriptorId) =>
        "{\"7\":{\"Enabled\":true,\"MetaData\":{" + BuildWebAuthnCredentialJson(0, descriptorId) + "}}}";

    private static string BuildWebAuthnCredentialJson(int index, string descriptorId = "AAAA")
    {
        // PublicKey/UserHandle are never cryptographically validated by the code paths these
        // fixtures exercise (no assertion/attestation is verified) - only their shape matters. Use
        // a real COSE_Key CBOR-encoded ECDSA P-256 public key (what Fido2NetLib actually stores)
        // instead of a placeholder, so the fixture matches production data.
        using var authenticator = new FakeWebAuthnAuthenticator();
        var publicKey = Convert.ToBase64String(authenticator.GetCosePublicKey());
        var userHandle = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        return $"\"Key{index}\":{{\"Name\":\"TestKey{index}\",\"Descriptor\":{{\"Id\":\"{descriptorId}\",\"Type\":0,\"Transports\":null}},\"PublicKey\":\"{publicKey}\",\"UserHandle\":\"{userHandle}\",\"SignatureCounter\":0,\"RegDate\":\"2024-01-01T00:00:00\",\"Migrated\":false,\"AaGuid\":\"00000000-0000-0000-0000-000000000000\"}}";
    }

    public static string BuildOrganizationDuoProvidersJson() =>
        "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"" + new string('s', 40)
        + "\",\"ClientId\":\"" + new string('c', 20) + "\",\"Host\":\"api-test.duosecurity.com\"}}}";

    // ---------------------------------------------------------------------
    // Repository state setters
    // ---------------------------------------------------------------------

    public static async Task SetUserTwoFactorProvidersJsonAsync(
        IUserRepository userRepository, string userEmail, string providersJson)
    {
        var user = (await userRepository.GetByEmailAsync(userEmail))!;
        user.TwoFactorProviders = providersJson;
        await userRepository.UpsertAsync(user);
    }

    public static async Task SetOrganizationTwoFactorProvidersJsonAsync(
        IOrganizationRepository organizationRepository, Guid organizationId, string providersJson)
    {
        var org = (await organizationRepository.GetByIdAsync(organizationId))!;
        org.TwoFactorProviders = providersJson;
        await organizationRepository.UpsertAsync(org);
    }

    public static async Task GrantPremiumAsync(IUserRepository userRepository, string userEmail)
    {
        var user = (await userRepository.GetByEmailAsync(userEmail))!;
        user.Premium = true;
        await userRepository.UpsertAsync(user);
    }

    // ---------------------------------------------------------------------
    // JSON response parsing
    // ---------------------------------------------------------------------

    // GET responses wrap provider state under a per-provider property (e.g. "duo": { "enabled": ... }).
    // The response models declare parameterized constructors that System.Text.Json cannot map for
    // deserialization, so the tests pull the fields they need structurally.
    public static async Task<(bool Enabled, string UserVerificationToken)> ReadEnabledAndUserVerificationTokenAsync(
        HttpResponseMessage response, string providerKey)
    {
        var root = await ReadJsonRootAsync(response);
        return (
            root.GetProperty(providerKey).GetProperty("enabled").GetBoolean(),
            root.GetProperty("userVerificationToken").GetString() ?? string.Empty);
    }

    public static async Task<JsonElement> ReadJsonRootAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    // ---------------------------------------------------------------------
    // HTTP body helpers (HttpClient.PutAsJsonAsync doesn't have a HttpMethod overload,
    // so we hand-roll Delete with a body)
    // ---------------------------------------------------------------------

    public static Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpClient client, HttpMethod method, string url, T body) =>
        SendRawJsonAsync(client, method, url, JsonSerializer.Serialize(body));

    public static async Task<HttpResponseMessage> SendRawJsonAsync(
        HttpClient client, HttpMethod method, string url, string json)
    {
        using var message = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return await client.SendAsync(message);
    }
}
