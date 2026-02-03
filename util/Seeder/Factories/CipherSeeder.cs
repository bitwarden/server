using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

/// <summary>
/// Creates encrypted ciphers for seeding vaults via the Rust SDK.
/// </summary>
/// <remarks>
/// Supported cipher types:
/// <list type="bullet">
///   <item><description>Login - <see cref="CreateOrganizationLoginCipher"/></description></item>
/// </list>
/// Future: Card, Identity, SecureNote will follow the same pattern—public Create method + private Transform method.
/// </remarks>
public class CipherSeeder
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

    public static Cipher CreateOrganizationLoginCipher(
        Guid organizationId,
        string orgKeyBase64,
        string name,
        string? username = null,
        string? password = null,
        string? uri = null,
        string? notes = null)
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
                Uris = string.IsNullOrEmpty(uri) ? null : [new LoginUriViewDto { Uri = uri }]
            }
        };

        return EncryptAndTransform(cipherView, orgKeyBase64, organizationId);
    }

    public static Cipher CreateOrganizationLoginCipherWithFields(
        Guid organizationId,
        string orgKeyBase64,
        string name,
        string? username,
        string? password,
        string? uri,
        IEnumerable<(string name, string value, int type)> fields)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Type = CipherTypes.Login,
            Login = new LoginViewDto
            {
                Username = username,
                Password = password,
                Uris = string.IsNullOrEmpty(uri) ? null : [new LoginUriViewDto { Uri = uri }]
            },
            Fields = fields.Select(f => new FieldViewDto
            {
                Name = f.name,
                Value = f.value,
                Type = f.type
            }).ToList()
        };

        return EncryptAndTransform(cipherView, orgKeyBase64, organizationId);
    }

    private static Cipher EncryptAndTransform(CipherViewDto cipherView, string keyBase64, Guid organizationId)
    {
        var viewJson = JsonSerializer.Serialize(cipherView, SdkJsonOptions);
        var encryptedJson = RustSdkService.EncryptCipher(viewJson, keyBase64);

        var encryptedDto = JsonSerializer.Deserialize<EncryptedCipherDto>(encryptedJson, SdkJsonOptions)
            ?? throw new InvalidOperationException("Failed to parse encrypted cipher");

        return TransformLoginToServerCipher(encryptedDto, organizationId);
    }

    private static Cipher TransformLoginToServerCipher(EncryptedCipherDto encrypted, Guid organizationId)
    {
        var loginData = new CipherLoginData
        {
            Name = encrypted.Name,
            Notes = encrypted.Notes,
            Username = encrypted.Login?.Username,
            Password = encrypted.Login?.Password,
            Totp = encrypted.Login?.Totp,
            PasswordRevisionDate = encrypted.Login?.PasswordRevisionDate,
            Uris = encrypted.Login?.Uris?.Select(u => new CipherLoginData.CipherLoginUriData
            {
                Uri = u.Uri,
                UriChecksum = u.UriChecksum,
                Match = u.Match.HasValue ? (UriMatchType?)u.Match : null
            }),
            Fields = encrypted.Fields?.Select(f => new CipherFieldData
            {
                Name = f.Name,
                Value = f.Value,
                Type = (FieldType)f.Type,
                LinkedId = f.LinkedId
            })
        };

        var dataJson = JsonSerializer.Serialize(loginData, ServerJsonOptions);

        return new Cipher
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            UserId = null,
            Type = CipherType.Login,
            Data = dataJson,
            Key = encrypted.Key,
            Reprompt = (CipherRepromptType?)encrypted.Reprompt,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
    }
}

