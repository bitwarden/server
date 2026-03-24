using System.Security.Cryptography;
using System.Text.Json;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class LoginCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        Guid? organizationId = null,
        Guid? userId = null,
        string? username = null,
        string? password = null,
        string? totp = null,
        string? uri = null,
        string? notes = null,
        bool reprompt = false,
        bool deleted = false,
        IEnumerable<(string name, string value, int type)>? fields = null,
        IEnumerable<(string rpName, string userName)>? passkeys = null
        )
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.Login,
            Login = new LoginViewDto
            {
                Username = username,
                Password = password,
                Totp = totp,
                Uris = string.IsNullOrEmpty(uri) ? null : [new LoginUriViewDto { Uri = uri }],
                Fido2Credentials = passkeys == null ? null : passkeys.Select(r => CreateFido2Credential(r.rpName, r.userName)).ToList()
            },
            Reprompt = reprompt ? RepromptTypes.Password : RepromptTypes.None,
            DeletedDate = deleted ? DateTime.UtcNow.AddDays(-1) : null,
            Fields = fields?.Select(f => new FieldViewDto
            {
                Name = f.name,
                Value = f.value,
                Type = f.type
            }).ToList()
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToLoginData(), CipherType.Login, organizationId, userId, deletedDate: cipherView.DeletedDate);
    }

    internal static Cipher CreateFromSeed(
        string encryptionKey,
        SeedVaultItem item,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = item.Name,
            Notes = item.Notes,
            Type = CipherTypes.Login,
            Login = item.Login == null ? null : new LoginViewDto
            {
                Username = item.Login.Username,
                Password = item.Login.Password,
                Totp = item.Login.Totp,
                Uris = item.Login.Uris?.Select(u => new LoginUriViewDto
                {
                    Uri = u.Uri,
                    Match = SeedItemMapping.MapUriMatchType(u.Match)
                }).ToList()
            },
            Fields = SeedItemMapping.MapFields(item.Fields)
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToLoginData(), CipherType.Login, organizationId, userId);
    }

    public static Fido2CredentialViewDto CreateFido2Credential(string rpName, string userName)
    {
        // Generate ECDSA P-256 private key in PKCS#8 format
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyValue = CoreHelpers.Base64UrlEncode(ecdsa.ExportPkcs8PrivateKey());

        // Generate 16-byte random user handle and encode as unpadded base64url
        var userHandleBytes = new byte[16];
        new Random().NextBytes(userHandleBytes);
        var userHandle = CoreHelpers.Base64UrlEncode(userHandleBytes);

        return new Fido2CredentialViewDto
        {
            Discoverable = JsonSerializer.Serialize(true),
            CredentialId = JsonSerializer.Serialize(Guid.NewGuid()),
            KeyValue = keyValue,
            Counter = "0",
            RpId = rpName,
            RpName = rpName,
            UserHandle = userHandle,
            UserName = userName,
            UserDisplayName = userName,
        };
    }
}
