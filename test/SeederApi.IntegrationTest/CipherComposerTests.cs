using Bit.Core.Vault.Entities;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
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
    public void BuildPasswordHistory_ZeroTotal_DoesNotThrow()
    {
        var history = CipherComposer.BuildPasswordHistory(0, 1, 0, PasswordDistributions.Realistic);

        Assert.Single(history);
        Assert.False(string.IsNullOrEmpty(history[0].Password));
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
