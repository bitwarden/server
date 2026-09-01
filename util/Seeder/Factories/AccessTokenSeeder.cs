using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

internal static class AccessTokenSeeder
{
    private const int _clientSecretLength = 30;

    /// <summary>
    /// Builds a Secrets Manager access-token <see cref="ApiKey"/> for a service account, plus the two
    /// plaintext pieces needed to assemble the token string. The final token
    /// (<c>0.{apiKeyId}.{clientSecret}:{encryptionKeyB64}</c>) is assembled by the caller after
    /// persistence, because the repository regenerates the Id on create.
    /// </summary>
    internal static (ApiKey ApiKey, string ClientSecret, string EncryptionKeyB64) Create(
        string organizationKeyB64, Guid serviceAccountId, string name, bool write)
    {
        var encryptionKey = RandomNumberGenerator.GetBytes(16);
        var encryptionKeyB64 = Convert.ToBase64String(encryptionKey);
        var derivedKeyB64 = Convert.ToBase64String(DeriveAccessTokenKey(encryptionKey));

        var clientSecret = CoreHelpers.SecureRandomString(_clientSecretLength);

        var payload = JsonSerializer.Serialize(new AccessTokenPayload { EncryptionKey = organizationKeyB64 });

        var apiKey = new ApiKey
        {
            Id = CombGuid.Generate(),
            ServiceAccountId = serviceAccountId,
            Name = write ? $"{name} (Read/Write)" : $"{name} (Read Only)",
            ClientSecretHash = HashClientSecret(clientSecret),
            Scope = "[\"api.secrets\"]",
            EncryptedPayload = RustSdkService.EncryptString(payload, derivedKeyB64),
            Key = RustSdkService.EncryptString(encryptionKeyB64, organizationKeyB64)
        };

        return (apiKey, clientSecret, encryptionKeyB64);
    }

    /// <summary>
    /// Reproduces the SDK's access-token key derivation: HMAC-SHA256 (key "bitwarden-accesstoken") over the
    /// 16-byte encryption key, then HKDF-Expand SHA256 to 64 bytes with info "sm-access-token". The result is
    /// an enc||mac composite key matching <c>derive_shareable_key</c> in bitwarden-crypto.
    /// </summary>
    internal static byte[] DeriveAccessTokenKey(byte[] encryptionKey)
    {
        var prk = HMACSHA256.HashData(Encoding.UTF8.GetBytes("bitwarden-accesstoken"), encryptionKey);
        return HKDF.Expand(HashAlgorithmName.SHA256, prk, 64, Encoding.UTF8.GetBytes("sm-access-token"));
    }

    private static string HashClientSecret(string clientSecret)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret)));
    }

    private sealed class AccessTokenPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("encryptionKey")]
        public required string EncryptionKey { get; init; }
    }
}
