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
            Type = CipherTypes.SshKey,
            SshKey = item.SshKey == null ? null : new SshKeyViewDto
            {
                PrivateKey = item.SshKey.PrivateKey,
                PublicKey = item.SshKey.PublicKey,
                Fingerprint = item.SshKey.KeyFingerprint
            }
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        var cipher = CipherEncryption.CreateEntity(encrypted, encrypted.ToSshKeyData(), CipherType.SSHKey, organizationId, userId);

        cipher.Reprompt = (CipherRepromptType?)item.Reprompt;

        return cipher;
    }
}
