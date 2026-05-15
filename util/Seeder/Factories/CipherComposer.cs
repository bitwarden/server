using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Models;

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
        Guid? userId = null,
        CipherRepromptType reprompt = CipherRepromptType.None)
    {
        return cipherType switch
        {
            CipherType.Login => ComposeLogin(index, encryptionKey, companies, generator, passwordDistribution, organizationId, userId, reprompt),
            CipherType.Card => ComposeCard(index, encryptionKey, generator, organizationId, userId, reprompt),
            CipherType.Identity => ComposeIdentity(index, encryptionKey, generator, organizationId, userId, reprompt),
            CipherType.SecureNote => ComposeSecureNote(index, encryptionKey, generator, organizationId, userId, reprompt),
            CipherType.SSHKey => ComposeSshKey(index, encryptionKey, organizationId, userId, reprompt),
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
        Guid? userId = null,
        CipherRepromptType reprompt = CipherRepromptType.None)
    {
        var company = companies[index % companies.Length];
        var uri = $"https://{company.Domain}";
        var username = generator.Username.GenerateByIndex(index, totalHint: generator.CipherCount, domain: company.Domain);

        // ~20% of logins get a FIDO2 passkey; ~40% get a 1-3 entry password history. Deterministic by index.
        var fido2Credentials = index % 5 == 0
            ? new List<Fido2CredentialViewDto> { LoginCipherSeeder.CreateFido2Credential(company.Name, username) }
            : null;

        var passwordHistory = index % 5 < 2
            ? BuildPasswordHistory(index, 1 + (index % 3), generator.CipherCount, passwordDistribution)
            : null;

        return LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = $"{company.Name} ({company.Category})",
            EncryptionKey = encryptionKey,
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt,
            Login = new LoginViewDto
            {
                Username = username,
                Password = Passwords.GetPassword(index, generator.CipherCount, passwordDistribution),
                Uris = [new LoginUriViewDto { Uri = uri }],
                Fido2Credentials = fido2Credentials,
                PasswordHistory = passwordHistory
            }
        });
    }

    private static List<PasswordHistoryViewDto> BuildPasswordHistory(
        int index,
        int entryCount,
        int total,
        Distribution<PasswordStrength> passwordDistribution)
    {
        var history = new List<PasswordHistoryViewDto>(entryCount);
        for (var k = 1; k <= entryCount; k++)
        {
            // Offset back into the password pool so prior entries are deterministic and distinct from the current password.
            var priorIndex = Math.Abs(index - (k * 7919));
            history.Add(new PasswordHistoryViewDto
            {
                Password = Passwords.GetPassword(priorIndex, total, passwordDistribution),
                LastUsedDate = DateTime.UtcNow.AddDays(-7 * k)
            });
        }
        return history;
    }

    private static Cipher ComposeCard(
        int index,
        string encryptionKey,
        GeneratorContext generator,
        Guid? organizationId = null,
        Guid? userId = null,
        CipherRepromptType reprompt = CipherRepromptType.None)
    {
        var card = generator.Card.GenerateByIndex(index);
        return CardCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Card,
            Name = $"{card.CardholderName}'s {card.Brand}",
            EncryptionKey = encryptionKey,
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt,
            Card = card
        });
    }

    private static Cipher ComposeIdentity(
        int index,
        string encryptionKey,
        GeneratorContext generator,
        Guid? organizationId = null,
        Guid? userId = null,
        CipherRepromptType reprompt = CipherRepromptType.None)
    {
        var identity = generator.Identity.GenerateByIndex(index);
        var name = $"{identity.FirstName} {identity.LastName}";
        if (!string.IsNullOrEmpty(identity.Company))
        {
            name += $" ({identity.Company})";
        }
        return IdentityCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Identity,
            Name = name,
            EncryptionKey = encryptionKey,
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt,
            Identity = identity
        });
    }

    private static Cipher ComposeSecureNote(
        int index,
        string encryptionKey,
        GeneratorContext generator,
        Guid? organizationId = null,
        Guid? userId = null,
        CipherRepromptType reprompt = CipherRepromptType.None)
    {
        var (name, notes) = generator.SecureNote.GenerateByIndex(index);
        return SecureNoteCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.SecureNote,
            Name = name,
            Notes = notes,
            EncryptionKey = encryptionKey,
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt
        });
    }

    private static Cipher ComposeSshKey(
        int index,
        string encryptionKey,
        Guid? organizationId = null,
        Guid? userId = null,
        CipherRepromptType reprompt = CipherRepromptType.None)
    {
        var sshKey = SshKeyDataGenerator.GenerateByIndex(index);
        return SshKeyCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.SSHKey,
            Name = $"SSH Key {index + 1}",
            EncryptionKey = encryptionKey,
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt,
            SshKey = sshKey
        });
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

    /// <summary>
    /// Builds the Folders JSON column value from a set of (userId, folderId) pairs.
    /// Produces <c>{"USERID1":"FOLDERID1","USERID2":"FOLDERID2"}</c> with uppercase GUIDs.
    /// </summary>
    internal static string BuildFoldersJson(Dictionary<Guid, Guid> userFolderMap)
    {
        var entries = userFolderMap.Select(kvp =>
            $"\"{kvp.Key.ToString().ToUpperInvariant()}\":\"{kvp.Value.ToString().ToUpperInvariant()}\"");
        return $"{{{string.Join(",", entries)}}}";
    }

    /// <summary>
    /// Builds the Favorites JSON column value from a set of user IDs.
    /// Produces <c>{"USERID1":true,"USERID2":true}</c> with uppercase GUIDs.
    /// </summary>
    internal static string BuildFavoritesJson(List<Guid> userIds)
    {
        var entries = userIds.Select(id =>
            $"\"{id.ToString().ToUpperInvariant()}\":true");
        return $"{{{string.Join(",", entries)}}}";
    }
}
