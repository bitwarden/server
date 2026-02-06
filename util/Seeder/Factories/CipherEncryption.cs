using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.RustSDK;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class CipherEncryption
{
    private static readonly JsonSerializerOptions SdkJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ServerJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static EncryptedCipherDto Encrypt(CipherViewDto cipherView, string keyBase64)
    {
        var viewJson = JsonSerializer.Serialize(cipherView, SdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptCipher(viewJson, keyBase64);
        return JsonSerializer.Deserialize<EncryptedCipherDto>(encryptedJson, SdkJsonOptions)
            ?? throw new InvalidOperationException("Failed to parse encrypted cipher");
    }

    internal static Cipher CreateEntity(
        EncryptedCipherDto encrypted,
        object data,
        CipherType cipherType,
        Guid? organizationId,
        Guid? userId)
    {
        var dataJson = JsonSerializer.Serialize(data, ServerJsonOptions);

        return new Cipher
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            UserId = userId,
            Type = cipherType,
            Data = dataJson,
            Key = encrypted.Key,
            Reprompt = (CipherRepromptType?)encrypted.Reprompt,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
    }
}
