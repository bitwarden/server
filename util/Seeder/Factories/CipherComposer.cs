using System.Globalization;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Generators;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

/// <summary>
/// Composes cipher entities from generated data, handling encryption and ownership assignment.
/// Used by generation steps to create realistic ciphers for organizations or personal vaults.
/// </summary>
internal static class CipherComposer
{
    internal const int PasskeysPer100Logins = 1;

    internal const int PasswordHistoryPer100Logins = 6;

    /// <summary>Items encrypted with their own key, wrapped by the org/user key. EU prod 2026-10-11: 55% in the target org, 33% fleet-wide.</summary>
    internal const int ItemKeysPer100 = 55;

    // Stride 37 spreads keyed items across the index range instead of one block per 100
    private static CipherEncryptionType EncryptionFor(int index) =>
        (index * 37 + 11) % 100 < ItemKeysPer100 ? CipherEncryptionType.CipherKey : CipherEncryptionType.UserKey;

    /// <param name="index">Position of the item; drives the generated text.</param>
    /// <param name="total">
    /// Size of the caller's index space, used for distribution selection (password strength, username category)
    /// at <c>index % total</c>. Must be positive.
    /// </param>
    internal static Cipher Compose(
        int index,
        int total,
        CipherType cipherType,
        string encryptionKey,
        Company[] companies,
        GeneratorContext generator,
        Distribution<PasswordStrength> passwordDistribution,
        Guid? organizationId = null,
        Guid? userId = null,
        CipherRepromptType reprompt = CipherRepromptType.None)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(total);

        return cipherType switch
        {
            CipherType.Login => ComposeLogin(index, total, encryptionKey, companies, generator, passwordDistribution, organizationId, userId, reprompt),
            CipherType.Card => ComposeCard(index, encryptionKey, generator, organizationId, userId, reprompt),
            CipherType.Identity => ComposeIdentity(index, encryptionKey, generator, organizationId, userId, reprompt),
            CipherType.SecureNote => ComposeSecureNote(index, encryptionKey, generator, organizationId, userId, reprompt),
            CipherType.SSHKey => ComposeSshKey(index, encryptionKey, organizationId, userId, reprompt),
            _ => throw new ArgumentException($"Unsupported cipher type: {cipherType}")
        };
    }

    private static Cipher ComposeLogin(
        int index,
        int total,
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
        var slot = index % total;
        var username = generator.Username.GenerateByIndex(slot, totalHint: total, domain: company.Domain);

        // Rates per 100 logins, deterministic by index, from EU prod (2026-10-11): passkeys on 0.2-1.2% of logins,
        // password history on 5-7% of items.
        var per100 = index % 100;
        var fido2Credentials = per100 < PasskeysPer100Logins
            ? new List<Fido2CredentialViewDto> { LoginCipherSeeder.CreateFido2Credential(company.Domain, company.Name, username) }
            : null;

        var passwordHistory = per100 >= 50 && per100 < 50 + PasswordHistoryPer100Logins
            ? BuildPasswordHistory(slot, 1 + (index % 3), total, passwordDistribution)
            : null;

        return LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = $"{company.Name} ({company.Category})",
            EncryptionKey = encryptionKey,
            CipherEncryption = EncryptionFor(index),
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt,
            Login = new LoginViewDto
            {
                Username = username,
                Password = Passwords.GetPassword(slot, total, passwordDistribution),
                Uris = [new LoginUriViewDto { Uri = uri }],
                Fido2Credentials = fido2Credentials,
                PasswordHistory = passwordHistory
            }
        });
    }

    internal static List<PasswordHistoryViewDto> BuildPasswordHistory(
        int index,
        int entryCount,
        int total,
        Distribution<PasswordStrength> passwordDistribution)
    {
        var history = new List<PasswordHistoryViewDto>(entryCount);
        for (var k = 1; k <= entryCount; k++)
        {
            // Walk to a deterministic prior position in the pool. The offset must stay in [0, total) so
            // Distribution.Select lands in a real bucket (otherwise every historical password collapses into the
            // strongest tier), and must be non-zero so priorIndex != index — i.e. the prior password is genuinely
            // distinct from the current one. 7919 is prime; multiplying by k varies offsets across entries.
            int priorIndex;
            if (total <= 1)
            {
                priorIndex = 0;
            }
            else
            {
                var offset = (k * 7919) % total;
                if (offset == 0)
                {
                    offset = 1;
                }
                priorIndex = (index + offset) % total;
            }
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
            CipherEncryption = EncryptionFor(index),
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
            CipherEncryption = EncryptionFor(index),
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
            CipherEncryption = EncryptionFor(index),
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
            CipherEncryption = EncryptionFor(index),
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
    /// Applies archived/deleted lifecycle state to a composed cipher per its membership in
    /// <paramref name="selection"/>. No-op if <paramref name="index"/> is in none of the sets.
    /// <paramref name="resolveArchiveOwner"/> is invoked only when the cipher is archived (alone or
    /// both), so callers can defer lookups (e.g. a dictionary keyed only by archived indices) that
    /// would otherwise be unsafe to evaluate eagerly.
    /// </summary>
    internal static void AssignArchiveOrDeleteState(Cipher cipher, int index, ArchiveDeleteSets selection, Func<int, Guid> resolveArchiveOwner)
    {
        var isBoth = selection.Both.Contains(index);
        var isArchived = isBoth || selection.Archived.Contains(index);

        // CreationDate must predate ArchivedDate/DeletedDate — Compose stamps it to UtcNow, which is
        // otherwise later than these backdated values.
        if (isBoth)
        {
            var archivedDate = DaysAgo(index, 15, 90);
            var deletedDate = DaysAgo(index, 7, 14);
            cipher.CreationDate = archivedDate;
            cipher.Archives = BuildArchivesJson(resolveArchiveOwner(index), archivedDate);
            cipher.DeletedDate = deletedDate;
            cipher.RevisionDate = deletedDate;
        }
        else if (isArchived)
        {
            var archivedDate = DaysAgo(index, 15, 90);
            cipher.CreationDate = archivedDate;
            cipher.Archives = BuildArchivesJson(resolveArchiveOwner(index), archivedDate);
            cipher.RevisionDate = archivedDate;
        }
        else if (selection.DeletedOnly.Contains(index))
        {
            var deletedDate = DaysAgo(index, 7, 14);
            cipher.CreationDate = deletedDate;
            cipher.DeletedDate = deletedDate;
            cipher.RevisionDate = deletedDate;
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

    /// <summary>
    /// Builds the Archives JSON column value for a single user. Mirrors the JSON_MODIFY shape
    /// produced by Cipher_Archive.sql. Produces <c>{"USERID":"yyyy-MM-ddTHH:mm:ss.fffZ"}</c>
    /// with an uppercase GUID key.
    /// </summary>
    internal static string BuildArchivesJson(Guid userId, DateTime archivedDateUtc)
    {
        var timestamp = archivedDateUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        return $"{{\"{userId.ToString().ToUpperInvariant()}\":\"{timestamp}\"}}";
    }

    /// <summary>
    /// Deterministic "N days ago" timestamp within [minDaysAgo, maxDaysAgo], varied by index.
    /// Mirrors the existing BuildPasswordHistory precedent of AddDays(-7 * k) — no RNG needed.
    /// </summary>
    internal static DateTime DaysAgo(int index, int minDaysAgo, int maxDaysAgo)
    {
        var range = maxDaysAgo - minDaysAgo + 1;
        var days = minDaysAgo + (index % range);
        return DateTime.UtcNow.AddDays(-days);
    }
}
