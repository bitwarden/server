using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class LoginCipherSeeder
{
    internal static Cipher Create(CipherSeed options)
    {
        AddUriChecksums(options.Login);

        var cipherView = new CipherViewDto
        {
            OrganizationId = options.OrganizationId,
            Name = options.Name,
            Notes = options.Notes,
            Type = CipherTypes.Login,
            Login = options.Login,
            Fields = options.Fields,
            Reprompt = (int)options.Reprompt
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, options.EncryptionKey!, options.CipherEncryption);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToLoginData(), CipherType.Login, options.OrganizationId, options.UserId);
    }

    /// <summary>
    /// Writes base64(SHA-256(uri)) as the checksum of every login URI that has none, as clients do.
    /// The SDK drops URIs whose checksum is missing or wrong on items with their own key, so without it
    /// those logins lose their URIs in every client.
    /// </summary>
    private static void AddUriChecksums(LoginViewDto? login)
    {
        foreach (var uri in (login?.Uris ?? []).Where(u => u.Uri is not null && u.UriChecksum is null))
        {
            uri.UriChecksum = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(uri.Uri!)));
        }
    }

    /// <summary>
    /// Builds a passkey, generating any key material not supplied. Supply the full set to seed a
    /// credential a relying party already has registered, so the vault can sign in with it.
    /// </summary>
    internal static Fido2CredentialViewDto CreateFido2Credential(
        string rpId,
        string rpName,
        string userName,
        string? credentialId = null,
        string? keyValue = null,
        string? userHandle = null,
        string? userDisplayName = null)
    {
        if (credentialId is not null && !IsClientParsableCredentialId(credentialId))
        {
            throw new ArgumentException(
                $"Passkey credential ID must be a GUID or \"b64.\" followed by the base64url ID, but was \"{credentialId}\".",
                nameof(credentialId));
        }

        return new Fido2CredentialViewDto
        {
            Discoverable = JsonSerializer.Serialize(true),
            // Clients parse a GUID string or a "b64."-prefixed ID; a JSON-serialized Guid keeps its quotes and fails to parse.
            CredentialId = credentialId ?? Guid.NewGuid().ToString(),
            KeyValue = keyValue ?? GeneratePrivateKey(),
            Counter = "0",
            RpId = rpId,
            RpName = rpName,
            UserHandle = userHandle ?? GenerateUserHandle(),
            UserName = userName,
            UserDisplayName = userDisplayName ?? userName,
        };
    }

    private static bool IsClientParsableCredentialId(string credentialId) =>
        Guid.TryParseExact(credentialId, "D", out _) ||
        (credentialId.StartsWith("b64.", StringComparison.Ordinal) && credentialId.Length > 4);

    /// <summary>ECDSA P-256 private key in PKCS#8, base64url-encoded.</summary>
    private static string GeneratePrivateKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return CoreHelpers.Base64UrlEncode(ecdsa.ExportPkcs8PrivateKey());
    }

    private static string GenerateUserHandle()
    {
        var userHandleBytes = new byte[16];
        RandomNumberGenerator.Fill(userHandleBytes);
        return CoreHelpers.Base64UrlEncode(userHandleBytes);
    }
}
