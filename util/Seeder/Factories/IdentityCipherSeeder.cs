using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class IdentityCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        IdentityViewDto identity,
        Guid? organizationId = null,
        Guid? userId = null,
        string? notes = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.Identity,
            Identity = identity
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToIdentityData(), CipherType.Identity, organizationId, userId);
    }
}
