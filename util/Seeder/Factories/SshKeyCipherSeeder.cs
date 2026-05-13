using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class SshKeyCipherSeeder
{
    internal static Cipher Create(CipherSeed options)
    {

        var cipherView = new CipherViewDto
        {
            OrganizationId = options.OrganizationId,
            Name = options.Name,
            Notes = options.Notes,
            Type = CipherTypes.SshKey,
            SshKey = options.SshKey,
            Fields = options.Fields,
            Reprompt = (int)options.Reprompt
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, options.EncryptionKey!);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToSshKeyData(), CipherType.SSHKey, options.OrganizationId, options.UserId);
    }

}
