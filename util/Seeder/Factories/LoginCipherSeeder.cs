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
}
