using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.RustSDK;
using Bit.Seeder.Attributes;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories.Vault;

internal static class CipherEncryption
{
    private static readonly JsonSerializerOptions _sdkJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions _serverJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string _fieldPathsJson =
        JsonSerializer.Serialize(EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>());

    internal static EncryptedCipherDto Encrypt(CipherViewDto cipherView, string keyBase64)
    {
        var viewJson = JsonSerializer.Serialize(cipherView, _sdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptFields(viewJson, _fieldPathsJson, keyBase64);
        return JsonSerializer.Deserialize<EncryptedCipherDto>(encryptedJson, _sdkJsonOptions)
            ?? throw new InvalidOperationException("Failed to parse encrypted cipher");
    }

    internal static Cipher CreateEntity(
        EncryptedCipherDto encrypted,
        object data,
        CipherType cipherType,
        Guid? organizationId,
        Guid? userId,
        DateTime? deletedDate = null)
    {
        var dataJson = JsonSerializer.Serialize(data, _serverJsonOptions);

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
            RevisionDate = DateTime.UtcNow,
            DeletedDate = deletedDate
        };
    }
}
