using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class SshKeyCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        SshKeyViewDto sshKey,
        Guid? organizationId = null,
        Guid? userId = null,
        string? notes = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.SshKey,
            SshKey = sshKey
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToSshKeyData(), CipherType.SSHKey, organizationId, userId);
    }
}
