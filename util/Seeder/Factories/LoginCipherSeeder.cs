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
        string? uri = null,
        string? notes = null,
        IEnumerable<(string name, string value, int type)>? fields = null)
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
            },
            Fields = fields?.Select(f => new FieldViewDto
            {
                Name = f.name,
                Value = f.value,
                Type = f.type
            }).ToList()
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToLoginData(), CipherType.Login, organizationId, userId);
    }

    internal static Cipher CreateFromSeed(
        string encryptionKey,
        SeedItem item,
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
}
