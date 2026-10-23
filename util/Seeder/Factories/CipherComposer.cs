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
/// <remarks>
/// The values of the <c>*Per100</c> constants and their sources are in
/// <c>Data/README.md#calibrated-login-rates</c>.
/// </remarks>
internal static class CipherComposer
{
    /// <summary>
    /// Logins per 100 that carry a FIDO2 passkey.
    /// </summary>
    internal const int PasskeysPer100Logins = 1;

    /// <summary>
    /// Logins per 100 that carry a password history.
    /// </summary>
    internal const int PasswordHistoryPer100Logins = 6;

    /// <summary>
    /// Logins per 100 that carry at least one URI.
    /// </summary>
    internal const int UriPer100Logins = 32;

    /// <summary>
    /// Logins per 100 that carry two URIs, counted inside <see cref="UriPer100Logins"/>.
    /// </summary>
    internal const int MultiUriPer100Logins = 1;

    /// <summary>
    /// Logins per 100 that carry notes.
    /// </summary>
    internal const int NotesPer100Logins = 37;

    /// <summary>
    /// Logins per 100 that carry custom fields.
    /// </summary>
    internal const int FieldsPer100Logins = 6;

    /// <summary>
    /// Logins per 100 that carry a TOTP secret.
    /// </summary>
    internal const int TotpPer100Logins = 2;

    /// <summary>
    /// Generated items per 100, of every type, encrypted with their own key wrapped by the org or user key.
    /// </summary>
    internal const int ItemKeysPer100 = 55;

    /// <summary>
    /// Distinct sites per 100 logins, rounded up to whole passes over the company list.
    /// </summary>
    internal const int AppsPer100Logins = 24;

    /// <summary>
    /// Logins per 100, among those drawn Fair or stronger, whose password is practically unique. VeryWeak and Weak
    /// draws always use the shared pools.
    /// </summary>
    internal const int UniquePasswordsPer100FairOrStronger = 80;

    /// <summary>
    /// Composes one encrypted cipher of the given type for an organization or a personal vault.
    /// </summary>
    /// <param name="index">
    /// Position of the item in the vault. Drives which items carry URIs, notes, custom fields, a TOTP secret, a
    /// passkey, a password history or their own item key, and the generated content of each, including a login's
    /// company, site pass and unique password. A login's username, password strength and pooled password come from
    /// <c>index % total</c> instead, so they repeat every <paramref name="total"/> items.
    /// May be at or above <paramref name="total"/>.
    /// </param>
    /// <param name="total">
    /// Modulus for a login's username, password strength and pooled password, and the vault size that
    /// <see cref="SiteDomain"/> uses to count site passes. The steps pass <see cref="CountLogins"/>, so logins cover
    /// the whole distribution. Must be positive.
    /// </param>
    /// <param name="cipherType">
    /// The type to compose: Login, Card, Identity, SecureNote or SSHKey. Any other type throws.
    /// </param>
    /// <param name="encryptionKey">
    /// The org key or user key that encrypts the cipher. For a cipher with its own item key, it wraps that key.
    /// </param>
    /// <param name="companies">
    /// The company pool. A login takes the company at <c>index % companies.Length</c> for its name, category and
    /// site; other types ignore it. Must not be empty for logins.
    /// </param>
    /// <param name="generator">
    /// The source of generated data: usernames, notes, custom fields, TOTP secrets, unique passwords, cards,
    /// identities and secure notes.
    /// </param>
    /// <param name="passwordDistribution">
    /// The mix of password strengths for logins. The slot <c>index % total</c> picks the strength, and with it the
    /// pooled password and the password history. Other types ignore it.
    /// </param>
    /// <param name="organizationId">
    /// The owning organization of an org cipher. Null for a personal-vault cipher.
    /// </param>
    /// <param name="userId">
    /// The owning user of a personal-vault cipher. Null for an org cipher.
    /// </param>
    /// <param name="reprompt">
    /// Whether opening the item asks for the master password again.
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
        var strength = passwordDistribution.Select(slot, total);
        var username = generator.Username.GenerateByIndex(slot, totalHint: total, domain: domain);

        // Offset 7 puts the passkey on a login that also has a URI, as real passkey logins do, and its own key
        var fido2Credentials = Spread(index, 31, 7) < PasskeysPer100Logins
            ? new List<Fido2CredentialViewDto> { LoginCipherSeeder.CreateFido2Credential(domain, company.Name, username) }
            : null;

        var passwordHistory = Spread(index, 41, 17) < PasswordHistoryPer100Logins
            ? BuildPasswordHistory(slot, 1 + (index % 3), total, passwordDistribution)
            : null;

        // Each pass over the companies moves a login's URI position by companies.Length * 9 % 100. A shift near 0 or
        // 100, as stride 7 gives, would leave most companies without a URI-bearing login
        List<LoginUriViewDto>? uris = Spread(index, 9, 3) switch
        {
            < MultiUriPer100Logins => [new LoginUriViewDto { Uri = $"https://{domain}" }, new LoginUriViewDto { Uri = $"https://login.{domain}" }],
            < UriPer100Logins => [new LoginUriViewDto { Uri = $"https://{domain}" }],
            _ => null
        };

        var noteRank = Spread(index, 13, 5);
        return LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = $"{company.Name} ({company.Category})",
            Notes = noteRank < NotesPer100Logins
                ? generator.LoginNotes.GenerateByIndex(index, noteRank, NotesPer100Logins, company.Category.ToString())
                : null,
            Fields = Spread(index, 17, 7) < FieldsPer100Logins ? generator.CustomField.GenerateByIndex(index) : null,
            EncryptionKey = encryptionKey,
            CipherEncryption = EncryptionFor(index),
            OrganizationId = organizationId,
            UserId = userId,
            Reprompt = reprompt,
            Login = new LoginViewDto
            {
                Username = username,
                Password = ComposePassword(index, slot, strength, generator),
                Totp = Spread(index, 23, 19) < TotpPer100Logins ? generator.TotpSecret.GenerateByIndex(index) : null,
                Uris = uris,
                Fido2Credentials = fido2Credentials,
                PasswordHistory = passwordHistory
            }
        });
    }

    private static string ComposePassword(
        int index,
        int slot,
        PasswordStrength strength,
        GeneratorContext generator) =>
        UniquePasswordGenerator.Supports(strength)
            && Spread(index, 43, 29) < UniquePasswordsPer100FairOrStronger
                ? generator.UniquePassword.GenerateByIndex(index, strength)
                : Passwords.GetPassword(slot, strength);

    // A stride coprime with 100 hits every residue once per block of 100 logins, so each rate is exact;
    // a different stride per content kind keeps the kinds from piling onto the same logins
    private static int Spread(int index, int stride, int offset) => (index * stride + offset) % 100;

    /// <summary>
    /// Picks the site of the login at <paramref name="index"/>. The site pool grows with the vault: about
    /// <see cref="AppsPer100Logins"/> sites per 100 logins, rounded up to whole passes over the company list.
    /// The first pass uses each company's domain; later passes use <c>{name}-{pass}.example</c>, so each pass adds
    /// distinct applications to the Access Intelligence report.
    /// </summary>
    /// <param name="company">
    /// The login's company. Its domain is the site on the first pass, and its first label names the site on later
    /// passes.
    /// </param>
    /// <param name="index">
    /// Position of the login. It picks the pass: each run of <paramref name="companyCount"/> logins is one pass, and
    /// the passes repeat once they are used up.
    /// </param>
    /// <param name="total">
    /// The number of logins, which sets how many passes there are: about <see cref="AppsPer100Logins"/> sites per
    /// 100 logins, rounded up to whole passes, and at least one.
    /// </param>
    /// <param name="companyCount">
    /// The size of the company list that one pass covers.
    /// </param>
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
