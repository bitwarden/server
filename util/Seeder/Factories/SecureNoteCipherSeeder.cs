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
}
