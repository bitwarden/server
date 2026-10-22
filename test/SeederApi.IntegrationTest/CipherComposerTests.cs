using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class CipherComposerTests
{
    [Fact]
    public void BuildPasswordHistory_IsDeterministic()
    {
        var first = CipherComposer.BuildPasswordHistory(42, 3, 1000, PasswordDistributions.Realistic);
        var second = CipherComposer.BuildPasswordHistory(42, 3, 1000, PasswordDistributions.Realistic);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Password, second[i].Password);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void BuildPasswordHistory_ReturnsRequestedEntryCount(int entryCount)
    {
        var history = CipherComposer.BuildPasswordHistory(10, entryCount, 1000, PasswordDistributions.Realistic);

        Assert.Equal(entryCount, history.Count);
    }

    [Fact]
    public void BuildPasswordHistory_EntriesWithinSameHistoryVary()
    {
        // Multiple entries on the same cipher should walk to different pool positions so the history is not
        // a string of identical passwords.
        var history = CipherComposer.BuildPasswordHistory(7, 3, 1000, PasswordDistributions.Realistic);

        Assert.Equal(3, history.Select(e => e.Password).Distinct().Count());
    }

    [Fact]
    public void BuildPasswordHistory_HistoricalPasswordsAreMostlyDistinctFromCurrent()
    {
        // The offset is designed to walk away from the current password's pool position. Within the same strength
        // bucket, modular collisions are still possible (priorIndex % poolSize == index % poolSize)
        const int total = 1000;
        const int entryCount = 3;
        var collisions = 0;
        var totalChecks = 0;

        for (var index = 0; index < total; index++)
        {
            var currentPassword = Passwords.GetPassword(index, total, PasswordDistributions.Realistic);
            var history = CipherComposer.BuildPasswordHistory(index, entryCount, total, PasswordDistributions.Realistic);
            foreach (var entry in history)
            {
                totalChecks++;
                if (entry.Password == currentPassword)
                {
                    collisions++;
                }
            }
        }

        Assert.True(collisions < totalChecks * 0.05,
            $"Expected fewer than 5% historical-vs-current collisions; got {collisions}/{totalChecks}.");
    }

    [Fact]
    public void BuildPasswordHistory_RespectsDistribution()
    {
        // priorIndex is constrained to [0, total), so historicals should span all five strength buckets
        const int total = 1000;
        const int entryCount = 3;
        var bucketsSeen = new HashSet<PasswordStrength>();

        for (var index = 0; index < total; index++)
        {
            var history = CipherComposer.BuildPasswordHistory(index, entryCount, total, PasswordDistributions.Realistic);
            foreach (var entry in history)
            {
                bucketsSeen.Add(ClassifyStrength(entry.Password));
            }
        }

        Assert.Equal(5, bucketsSeen.Count);
    }

    [Fact]
    public void BuildPasswordHistory_ZeroEntryCount_ReturnsEmptyList()
    {
        var history = CipherComposer.BuildPasswordHistory(10, 0, 1000, PasswordDistributions.Realistic);

        Assert.Empty(history);
    }

    [Fact]
    public void BuildPasswordHistory_ZeroTotal_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CipherComposer.BuildPasswordHistory(0, 1, 0, PasswordDistributions.Realistic));
    }

    [Fact]
    public void CountLogins_RealisticMix_ReturnsLoginShareOfTotal()
    {
        Assert.Equal(60, CipherComposer.CountLogins(CipherTypeDistributions.Realistic, 100));
    }

    [Fact]
    public void CountLogins_MixWithoutLogins_ReturnsOne()
    {
        var withoutLogins = new Distribution<CipherType>((CipherType.Card, 0.50), (CipherType.SecureNote, 0.50));

        Assert.Equal(1, CipherComposer.CountLogins(withoutLogins, 100));
    }

    [Fact]
    public void BuildArchivesJson_ProducesExpectedShape()
    {
        // Uses a lowercase GUID with hex letters (not just digits) so the assertion actually
        // exercises ToUpperInvariant() rather than passing regardless of casing.
        var userId = Guid.Parse("aabbccdd-eeff-1122-3344-5566778899aa");
        var archivedDate = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);

        var json = CipherComposer.BuildArchivesJson(userId, archivedDate);

        Assert.Equal(
            "{\"AABBCCDD-EEFF-1122-3344-5566778899AA\":\"2026-01-02T03:04:05.678Z\"}",
            json);
    }

    [Theory]
    [InlineData(7, 14)]
    [InlineData(15, 90)]
    public void DaysAgo_AlwaysWithinRequestedRange(int minDaysAgo, int maxDaysAgo)
    {
        var now = DateTime.UtcNow;

        for (var index = 0; index < 200; index++)
        {
            var result = CipherComposer.DaysAgo(index, minDaysAgo, maxDaysAgo);
            var daysAgo = (now - result).TotalDays;

            Assert.True(daysAgo >= minDaysAgo - 0.01 && daysAgo <= maxDaysAgo + 0.01,
                $"index {index}: expected {minDaysAgo}-{maxDaysAgo} days ago, got {daysAgo:F2}.");
        }
    }

    [Fact]
    public void DaysAgo_ArchivedOnlyRangeNeverOverlapsDeletedRange()
    {
        // The 15-90 (archived-only) and 7-14 (deleted) windows must never overlap, so an archived
        // date is always chronologically before a deleted date for the same index — required for
        // "both" ciphers where DeletedDate must postdate the Archives timestamp.
        for (var index = 0; index < 200; index++)
        {
            var archivedDate = CipherComposer.DaysAgo(index, 15, 90);
            var deletedDate = CipherComposer.DaysAgo(index, 7, 14);

            Assert.True(archivedDate < deletedDate,
                $"index {index}: archivedDate ({archivedDate:o}) must be before deletedDate ({deletedDate:o}).");
        }
    }

    [Fact]
    public void AssignArchiveOrDeleteState_IsBoth_SetsAllFourFields_ArchivedDateBeforeDeletedDate()
    {
        var cipher = new Cipher { CreationDate = DateTime.UtcNow };
        var ownerId = Guid.NewGuid();
        var selection = new ArchiveDeleteSets(
            Archived: [3], Both: [3], DeletedOnly: [], ArchivedOrder: [3]);

        CipherComposer.AssignArchiveOrDeleteState(cipher, 3, selection, _ => ownerId);

        Assert.NotNull(cipher.Archives);
        Assert.NotNull(cipher.DeletedDate);
        Assert.Equal(cipher.DeletedDate, cipher.RevisionDate);
        Assert.True(cipher.CreationDate < cipher.DeletedDate);
        Assert.Contains(ownerId.ToString().ToUpperInvariant(), cipher.Archives);
    }

    [Fact]
    public void AssignArchiveOrDeleteState_ArchivedOnly_SetsArchivesButLeavesDeletedDateNull()
    {
        var cipher = new Cipher { CreationDate = DateTime.UtcNow };
        var ownerId = Guid.NewGuid();
        var selection = new ArchiveDeleteSets(
            Archived: [3], Both: [], DeletedOnly: [], ArchivedOrder: [3]);

        CipherComposer.AssignArchiveOrDeleteState(cipher, 3, selection, _ => ownerId);

        Assert.NotNull(cipher.Archives);
        Assert.Null(cipher.DeletedDate);
        Assert.Equal(cipher.CreationDate, cipher.RevisionDate);
    }

    [Fact]
    public void AssignArchiveOrDeleteState_DeletedOnly_SetsDeletedDateButLeavesArchivesNull()
    {
        var cipher = new Cipher { CreationDate = DateTime.UtcNow };
        var selection = new ArchiveDeleteSets(
            Archived: [], Both: [], DeletedOnly: [3], ArchivedOrder: []);

        CipherComposer.AssignArchiveOrDeleteState(
            cipher, 3, selection, _ => throw new InvalidOperationException("must not be called"));

        Assert.Null(cipher.Archives);
        Assert.NotNull(cipher.DeletedDate);
        Assert.Equal(cipher.DeletedDate, cipher.RevisionDate);
    }

    [Fact]
    public void AssignArchiveOrDeleteState_UntouchedIndex_MutatesNothing()
    {
        var originalCreationDate = DateTime.UtcNow;
        var cipher = new Cipher { CreationDate = originalCreationDate };
        var originalRevisionDate = cipher.RevisionDate;
        var selection = new ArchiveDeleteSets(
            Archived: [], Both: [], DeletedOnly: [], ArchivedOrder: []);

        CipherComposer.AssignArchiveOrDeleteState(
            cipher, 3, selection, _ => throw new InvalidOperationException("must not be called"));

        Assert.Equal(originalCreationDate, cipher.CreationDate);
        Assert.Equal(originalRevisionDate, cipher.RevisionDate);
        Assert.Null(cipher.Archives);
        Assert.Null(cipher.DeletedDate);
    }

    [Fact]
    public void Compose_Logins_ContentRatesMatchTheConstants()
    {
        // Every stride is coprime with 100 and the total is a multiple of 100, so each count is exact
        const int total = 1_000;
        var logins = ComposeItems(CipherType.Login, total, RustSdkService.GenerateOrganizationKeys().Key)
            .Select(cipher => JsonSerializer.Deserialize<CipherLoginData>(cipher.Data)!)
            .ToList();

        Assert.Equal(CipherComposer.PasskeysPer100Logins * total / 100, logins.Count(login => login.Fido2Credentials is { Length: > 0 }));
        Assert.Equal(CipherComposer.PasswordHistoryPer100Logins * total / 100, logins.Count(login => login.PasswordHistory?.Any() == true));
        Assert.Equal(CipherComposer.UriPer100Logins * total / 100, logins.Count(login => login.Uris?.Any() == true));
        Assert.Equal(CipherComposer.MultiUriPer100Logins * total / 100, logins.Count(login => login.Uris?.Count() == 2));
        Assert.Equal(CipherComposer.NotesPer100Logins * total / 100, logins.Count(login => login.Notes is not null));
        Assert.Equal(CipherComposer.FieldsPer100Logins * total / 100, logins.Count(login => login.Fields?.Any() == true));
        Assert.Equal(CipherComposer.TotpPer100Logins * total / 100, logins.Count(login => login.Totp is not null));
    }

    [Fact]
    public void Compose_Login_StoresUrisWithoutTheLegacyTopLevelUri()
    {
        var withUris = ComposeItems(CipherType.Login, 100, RustSdkService.GenerateOrganizationKeys().Key)
            .Select(cipher => JsonNode.Parse(cipher.Data)!.AsObject())
            .Where(data => data.ContainsKey(nameof(CipherLoginData.Uris)))
            .ToList();

        Assert.NotEmpty(withUris);
        Assert.All(withUris, data => Assert.False(data.ContainsKey(nameof(CipherLoginData.Uri))));
    }

    [Theory]
    [InlineData(CipherType.Login, 1_000)]
    [InlineData(CipherType.Card, 100)]
    public void Compose_Items_FiftyFivePercentHaveTheirOwnKey(CipherType cipherType, int total)
    {
        var items = ComposeItems(cipherType, total, RustSdkService.GenerateOrganizationKeys().Key);

        Assert.Equal(CipherComposer.ItemKeysPer100 * total / 100, items.Count(cipher => cipher.Key is not null));
    }

    [Fact]
    public void SiteDomain_SmallVault_KeepsCompanyDomains()
    {
        // 1,312 logins is the largest vault whose 24-per-100 site target still fits in one pass over 314 companies
        const int total = 1_312;
        var companies = Companies.All;

        Assert.All(Enumerable.Range(0, total), index =>
        {
            var company = companies[index % companies.Length];
            Assert.Equal(company.Domain, CipherComposer.SiteDomain(company, index, total, companies.Length));
        });
    }

    [Fact]
    public void SiteDomain_VaultPastOnePass_GivesSecondPassItsOwnDomains()
    {
        var companies = Companies.All;
        var company = companies[0];

        var domain = CipherComposer.SiteDomain(company, companies.Length, 1_313, companies.Length);

        Assert.Equal($"{company.Domain.Split('.')[0]}-1.example", domain);
    }

    [Fact]
    public void Compose_OnePassVault_GivesEveryCompanyAUriBearingLogin()
    {
        // Logins pick their company by index % companies.Length, so the URI-bearing indices name the companies covered
        const int total = 1_312;
        var companies = Companies.All;

        var companiesWithUris = ComposeItems(CipherType.Login, total, RustSdkService.GenerateOrganizationKeys().Key)
            .Select((cipher, index) => (cipher, index))
            .Where(login => JsonNode.Parse(login.cipher.Data)!.AsObject().ContainsKey(nameof(CipherLoginData.Uris)))
            .Select(login => login.index % companies.Length)
            .Distinct()
            .Count();

        Assert.Equal(companies.Length, companiesWithUris);
    }

    [Fact]
    public void Compose_LargeVault_SpreadsLoginsOverMoreSitesThanTheCompanyList()
    {
        const int total = 20_000;
        var orgKey = RustSdkService.GenerateOrganizationKeys().Key;

        var hosts = ComposeItems(CipherType.Login, total, orgKey)
            .Where(cipher => cipher.Key is null)
            .Select(cipher => JsonSerializer.Deserialize<CipherLoginData>(cipher.Data)!.Uris?.FirstOrDefault()?.Uri)
            .OfType<string>()
            .Select(encrypted => new Uri(RustSdkService.DecryptString(encrypted, orgKey)).Host)
            .Distinct()
            .Count();

        Assert.True(hosts > Companies.All.Length * 3, $"Expected more than {Companies.All.Length * 3} distinct sites; got {hosts}.");
    }

    private static List<Cipher> ComposeItems(CipherType cipherType, int total, string orgKey)
    {
        var generator = GeneratorContext.FromOptions(new OrganizationVaultOptions
        {
            Name = "Composer Test",
            Domain = "composer.example",
            Users = 0
        });
        var organizationId = Guid.NewGuid();

        return Enumerable.Range(0, total)
            .Select(index => CipherComposer.Compose(
                index, total, cipherType, orgKey, Companies.All, generator, PasswordDistributions.Realistic,
                organizationId: organizationId))
            .ToList();
    }

    private static PasswordStrength ClassifyStrength(string password)
    {
        if (Array.IndexOf(Passwords.VeryWeak, password) >= 0) return PasswordStrength.VeryWeak;
        if (Array.IndexOf(Passwords.Weak, password) >= 0) return PasswordStrength.Weak;
        if (Array.IndexOf(Passwords.Fair, password) >= 0) return PasswordStrength.Fair;
        if (Array.IndexOf(Passwords.Strong, password) >= 0) return PasswordStrength.Strong;
        if (Array.IndexOf(Passwords.VeryStrong, password) >= 0) return PasswordStrength.VeryStrong;
        throw new InvalidOperationException($"Password '{password}' not found in any strength bucket.");
    }
}
