using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Data.Static;

namespace Bit.Seeder.Factories;

/// <summary>
/// Composes cipher entities from generated data, handling encryption and ownership assignment.
/// Used by generation steps to create realistic ciphers for organizations or personal vaults.
/// </summary>
internal static class CipherComposer
{
    internal static Cipher Compose(
        int index,
        CipherType cipherType,
        string encryptionKey,
        Company[] companies,
        GeneratorContext generator,
        Distribution<PasswordStrength> passwordDistribution,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        return cipherType switch
        {
            CipherType.Login => ComposeLogin(index, encryptionKey, companies, generator, passwordDistribution, organizationId, userId),
            CipherType.Card => ComposeCard(index, encryptionKey, generator, organizationId, userId),
            CipherType.Identity => ComposeIdentity(index, encryptionKey, generator, organizationId, userId),
            CipherType.SecureNote => ComposeSecureNote(index, encryptionKey, generator, organizationId, userId),
            CipherType.SSHKey => ComposeSshKey(index, encryptionKey, organizationId, userId),
            _ => throw new ArgumentException($"Unsupported cipher type: {cipherType}")
        };
    }

    private static Cipher ComposeLogin(
        int index,
        string encryptionKey,
        Company[] companies,
        GeneratorContext generator,
        Distribution<PasswordStrength> passwordDistribution,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        var company = companies[index % companies.Length];
        return LoginCipherSeeder.Create(
            encryptionKey,
            name: $"{company.Name} ({company.Category})",
            organizationId: organizationId,
            userId: userId,
            username: generator.Username.GenerateByIndex(index, totalHint: generator.CipherCount, domain: company.Domain),
            password: Passwords.GetPassword(index, generator.CipherCount, passwordDistribution),
            uri: $"https://{company.Domain}");
    }

    private static Cipher ComposeCard(
        int index,
        string encryptionKey,
        GeneratorContext generator,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        var card = generator.Card.GenerateByIndex(index);
        return CardCipherSeeder.Create(
            encryptionKey,
            name: $"{card.CardholderName}'s {card.Brand}",
            card: card,
            organizationId: organizationId,
            userId: userId);
    }

    private static Cipher ComposeIdentity(
        int index,
        string encryptionKey,
        GeneratorContext generator,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        var identity = generator.Identity.GenerateByIndex(index);
        var name = $"{identity.FirstName} {identity.LastName}";
        if (!string.IsNullOrEmpty(identity.Company))
        {
            name += $" ({identity.Company})";
        }
        return IdentityCipherSeeder.Create(
            encryptionKey,
            name: name,
            identity: identity,
            organizationId: organizationId,
            userId: userId);
    }

    private static Cipher ComposeSecureNote(
        int index,
        string encryptionKey,
        GeneratorContext generator,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        var (name, notes) = generator.SecureNote.GenerateByIndex(index);
        return SecureNoteCipherSeeder.Create(
            encryptionKey,
            name: name,
            organizationId: organizationId,
            userId: userId,
            notes: notes);
    }

    private static Cipher ComposeSshKey(
        int index,
        string encryptionKey,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        var sshKey = SshKeyDataGenerator.GenerateByIndex(index);
        return SshKeyCipherSeeder.Create(
            encryptionKey,
            name: $"SSH Key {index + 1}",
            sshKey: sshKey,
            organizationId: organizationId,
            userId: userId);
    }

    /// <summary>
    /// Assigns a folder to a cipher via round-robin selection from the user's folder list.
    /// </summary>
    internal static void AssignFolder(Cipher cipher, Guid userId, int index, Dictionary<Guid, List<Guid>> userFolderIds)
    {
        if (userFolderIds.TryGetValue(userId, out var folderIds) && folderIds.Count > 0)
        {
            cipher.Folders = $"{{\"{userId.ToString().ToUpperInvariant()}\":\"{folderIds[index % folderIds.Count].ToString().ToUpperInvariant()}\"}}";
        }
    }
}
