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
