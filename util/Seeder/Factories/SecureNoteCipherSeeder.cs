using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class SecureNoteCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        Guid? organizationId = null,
        Guid? userId = null,
        string? notes = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.SecureNote,
            SecureNote = new SecureNoteViewDto { Type = 0 }
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToSecureNoteData(), CipherType.SecureNote, organizationId, userId);
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
            Type = CipherTypes.SecureNote,
            SecureNote = new SecureNoteViewDto { Type = 0 },
            Fields = SeedItemMapping.MapFields(item.Fields)
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToSecureNoteData(), CipherType.SecureNote, organizationId, userId);
    }
}
