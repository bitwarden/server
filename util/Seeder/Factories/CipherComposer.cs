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
    /// <summary>
    /// Logins per 100 that carry a FIDO2 passkey. EU prod 2026-10-11: 0.2-1.2% of logins.
    /// </summary>
    internal const int PasskeysPer100Logins = 1;

    /// <summary>
    /// Logins per 100 that carry a password history. EU prod 2026-10-11: 5-7% of items.
    /// </summary>
    internal const int PasswordHistoryPer100Logins = 6;

    /// <summary>
    /// Logins per 100 that carry at least one URI. EU prod 2026-10-11, target org (fleet: 86%).
    /// </summary>
    internal const int UriPer100Logins = 32;

    /// <summary>
    /// Logins per 100 that carry two URIs, counted inside <see cref="UriPer100Logins"/>.
    /// EU prod 2026-10-11, target org: 0.9% (fleet: 2.4%).
    /// </summary>
    internal const int MultiUriPer100Logins = 1;

    /// <summary>
    /// Logins per 100 that carry notes. EU prod 2026-10-11, target org (fleet: 8%).
    /// </summary>
    internal const int NotesPer100Logins = 37;

    /// <summary>
    /// Logins per 100 that carry custom fields. EU prod 2026-10-11, target org (fleet: 4%).
    /// </summary>
    internal const int FieldsPer100Logins = 6;

    /// <summary>
    /// Logins per 100 that carry a TOTP secret. EU prod 2026-10-11, target org: 1.9% (fleet: 2.1%).
    /// </summary>
    internal const int TotpPer100Logins = 2;

    /// <summary>
    /// Generated items per 100, of every type, encrypted with their own key wrapped by the org or user key.
    /// EU prod 2026-10-11, target org (fleet: 33%).
    /// </summary>
    internal const int ItemKeysPer100 = 55;

    /// <summary>
    /// Distinct sites per 100 logins, rounded up to whole passes over the company list.
    /// Large-org median of applications per password in the Access Intelligence report.
    /// </summary>
    internal const int AppsPer100Logins = 24;

    /// <summary>
    /// Logins per 100, among those drawn Fair or stronger, whose password no other login shares. VeryWeak and Weak
    /// draws always use the shared pools: zxcvbn scores no unique password as low as the pooled ones, and reused
    /// passwords skew weak. An assumption, not a measurement: vault passwords are encrypted per item, so prod reuse
    /// can't be counted.
    /// </summary>
    internal const int UniquePasswordsPer100FairOrStronger = 80;

    /// <summary>
    /// Composes one encrypted cipher of the given type for an organization or a personal vault.
    /// </summary>
    /// <param name="index">
    /// Position of the item. A login's username and pooled password come from <c>index % total</c>, so they repeat
    /// every <paramref name="total"/> items; the other cipher types use <paramref name="index"/> directly.
    /// May be at or above <paramref name="total"/>.
    /// </param>
    /// <param name="total">
    /// Modulus for a login's username and password. The steps pass <see cref="CountLogins"/>, so logins cover
    /// the whole distribution. Must be positive.
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

    /// <summary>
    /// Counts the logins <paramref name="typeDistribution"/> assigns across <paramref name="total"/> items.
    /// The steps pass this as the <c>total</c> of <see cref="Compose"/>: the type distribution hands logins out
    /// as one contiguous run, so a larger total would give them only the first buckets of the password and
    /// username distributions. Never less than 1, because <see cref="Compose"/> rejects a total of zero and a
    /// mix without logins never uses the value.
    /// </summary>
    internal static int CountLogins(Distribution<CipherType> typeDistribution, int total)
    {
        var logins = typeDistribution.GetCounts(total)
            .Where(bucket => bucket.Value == CipherType.Login)
            .Sum(bucket => bucket.Count);

        return Math.Max(1, logins);
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
        var domain = SiteDomain(company, index, total, companies.Length);
        var slot = index % total;
        var username = generator.Username.GenerateByIndex(slot, totalHint: total, domain: domain);

        var per100 = index % 100;
        var fido2Credentials = per100 < PasskeysPer100Logins
            ? new List<Fido2CredentialViewDto> { LoginCipherSeeder.CreateFido2Credential(domain, company.Name, username) }
            : null;

        var passwordHistory = per100 >= 50 && per100 < 50 + PasswordHistoryPer100Logins
            ? BuildPasswordHistory(slot, 1 + (index % 3), total, passwordDistribution)
            : null;

        // Each pass over the companies moves a login's URI position by companies.Length * 9 % 100 (26 for 314),
        // so every company gets URI-bearing logins; stride 7 moved it by 2 and left 193 of 314 companies without one
        List<LoginUriViewDto>? uris = Spread(index, 9, 3) switch
        {
            < MultiUriPer100Logins => [new LoginUriViewDto { Uri = $"https://{domain}" }, new LoginUriViewDto { Uri = $"https://login.{domain}" }],
            < UriPer100Logins => [new LoginUriViewDto { Uri = $"https://{domain}" }],
            _ => null
        };

        return LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = $"{company.Name} ({company.Category})",
            Notes = Spread(index, 13, 5) < NotesPer100Logins ? LoginExtras.Notes(index, company.Category.ToString()) : null,
            Fields = Spread(index, 17, 7) < FieldsPer100Logins ? LoginExtras.Fields(index) : null,
            EncryptionKey = encryptionKey,
            CipherEncryption = EncryptionFor(index),
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt,
            Login = new LoginViewDto
            {
                Username = username,
                Password = ComposePassword(index, slot, total, passwordDistribution),
                Totp = Spread(index, 23, 19) < TotpPer100Logins ? LoginExtras.TotpSecret(index) : null,
                Uris = uris,
                Fido2Credentials = fido2Credentials,
                PasswordHistory = passwordHistory
            }
        });
    }

    private static string ComposePassword(int index, int slot, int total, Distribution<PasswordStrength> passwordDistribution)
    {
        var strength = passwordDistribution.Select(slot, total);
        return strength is not (PasswordStrength.VeryWeak or PasswordStrength.Weak)
            && Spread(index, 43, 29) < UniquePasswordsPer100FairOrStronger
                ? UniquePasswordGenerator.Generate(index, strength)
                : Passwords.GetPassword(slot, total, passwordDistribution);
    }

    // A stride coprime with 100 hits every residue once per block of 100 logins, so each rate is exact;
    // a different stride per content kind keeps the kinds from piling onto the same logins
    private static int Spread(int index, int stride, int offset) => (index * stride + offset) % 100;

    /// <summary>
    /// Picks the site of the login at <paramref name="index"/>. The site pool grows with the vault: about
    /// <see cref="AppsPer100Logins"/> sites per 100 logins, rounded up to whole passes over the company list.
    /// The first pass uses each company's domain; later passes use <c>{name}-{pass}.example</c>, so each pass adds
    /// distinct applications to the Access Intelligence report.
    /// </summary>
    /// <param name="total">The number of logins, which sets how many passes there are.</param>
    internal static string SiteDomain(Company company, int index, int total, int companyCount)
    {
        var passes = Math.Max(1, (total * AppsPer100Logins / 100 + companyCount - 1) / companyCount);
        var pass = index % (passes * companyCount) / companyCount;
        return pass == 0 ? company.Domain : $"{company.Domain.Split('.')[0]}-{pass}.example";
    }

    // Each cipher type is one contiguous run of indices, so a stride on the raw index reaches every type
    private static CipherEncryptionType EncryptionFor(int index) =>
        Spread(index, 37, 11) < ItemKeysPer100 ? CipherEncryptionType.CipherKey : CipherEncryptionType.UserKey;

    internal static List<PasswordHistoryViewDto> BuildPasswordHistory(
        int index,
        int entryCount,
        int total,
        Distribution<PasswordStrength> passwordDistribution)
    {
        var history = new List<PasswordHistoryViewDto>(entryCount);
        for (var k = 1; k <= entryCount; k++)
        {
            // Walk to a deterministic prior position in the pool. The offset must stay in [0, total) because
            // Distribution.Select throws outside that range, and must be non-zero so priorIndex != index — i.e. the
            // prior password is genuinely distinct from the current one. 7919 is prime; multiplying by k varies
            // offsets across entries.
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
