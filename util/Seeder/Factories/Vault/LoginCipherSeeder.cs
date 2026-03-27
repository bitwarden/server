using System.Security.Cryptography;
using System.Text.Json;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories.Vault;

internal static class LoginCipherSeeder
{
    internal static Cipher Create(CipherSeed options)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.EncryptionKey);

        var cipherView = new CipherViewDto
        {
            OrganizationId = options.OrganizationId,
            Name = options.Name,
            Notes = options.Notes,
            Type = CipherTypes.Login,
            Login = options.Login,
            Fields = options.Fields
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, options.EncryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToLoginData(), CipherType.Login, options.OrganizationId, options.UserId);
    }

    internal static Fido2CredentialViewDto CreateFido2Credential(string rpName, string userName)
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
