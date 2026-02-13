using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class SeedReaderTests
{
    private readonly SeedReader _reader = new();

    [Fact]
    public void ListAvailable_ReturnsAllSeedFiles()
    {
        var available = _reader.ListAvailable();

        Assert.Contains("ciphers.autofill-testing", available);
        Assert.Contains("ciphers.public-site-logins", available);
        Assert.Contains("organizations.dunder-mifflin", available);
        Assert.Contains("rosters.dunder-mifflin", available);
        Assert.Contains("presets.dunder-mifflin-full", available);
        Assert.Contains("presets.large-enterprise", available);
        Assert.Equal(6, available.Count);
    }

    [Fact]
    public void Read_AutofillTesting_DeserializesAllItems()
    {
        var seedFile = _reader.Read<SeedFile>("ciphers.autofill-testing");

        Assert.Equal(18, seedFile.Items.Count);

        var types = seedFile.Items.Select(i => i.Type).Distinct().OrderBy(t => t).ToList();
        Assert.Contains("login", types);
        Assert.Contains("card", types);
        Assert.Contains("identity", types);

        var logins = seedFile.Items.Where(i => i.Type == "login").ToList();
        Assert.All(logins, l => Assert.NotEmpty(l.Login!.Uris!));
    }

    [Fact]
    public void Read_PublicSiteLogins_DeserializesAllItems()
    {
        var seedFile = _reader.Read<SeedFile>("ciphers.public-site-logins");

        Assert.True(seedFile.Items.Count >= 90,
            $"Expected at least 90 public site logins, got {seedFile.Items.Count}");
    }

    [Fact]
    public void Read_NonExistentSeed_ThrowsWithAvailableList()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _reader.Read<SeedFile>("does-not-exist"));

        Assert.Contains("does-not-exist", ex.Message);
        Assert.Contains("ciphers.autofill-testing", ex.Message);
    }

    [Fact]
    public void Read_CipherSeeds_ItemNamesAreUnique()
    {
        var cipherSeeds = _reader.ListAvailable()
            .Where(n => n.StartsWith("ciphers."));

        foreach (var seedName in cipherSeeds)
        {
            var seedFile = _reader.Read<SeedFile>(seedName);
            var duplicates = seedFile.Items
                .GroupBy(i => i.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0,
                $"Seed '{seedName}' has duplicate item names: {string.Join(", ", duplicates)}");
        }
    }

    [Fact]
    public void Read_DunderMifflin_DeserializesOrganization()
    {
        var org = _reader.Read<SeedOrganization>("organizations.dunder-mifflin");

        Assert.Equal("Dunder Mifflin", org.Name);
        Assert.Equal("dundermifflin.com", org.Domain);
        Assert.Equal(70, org.Seats);
    }

    [Fact]
    public void Read_DunderMifflinRoster_DeserializesRoster()
    {
        var roster = _reader.Read<SeedRoster>("rosters.dunder-mifflin");

        Assert.Equal(58, roster.Users.Count);
        Assert.NotNull(roster.Groups);
        Assert.Equal(14, roster.Groups.Count);
        Assert.NotNull(roster.Collections);
        Assert.Equal(15, roster.Collections.Count);

        // Verify no duplicate email prefixes
        var prefixes = roster.Users
            .Select(u => $"{u.FirstName}.{u.LastName}".ToLowerInvariant())
            .ToList();
        Assert.Equal(prefixes.Count, prefixes.Distinct().Count());

        // Verify all group members reference valid users
        var prefixSet = new HashSet<string>(prefixes, StringComparer.OrdinalIgnoreCase);
        foreach (var group in roster.Groups)
        {
            Assert.All(group.Members, m => Assert.Contains(m, prefixSet));
        }

        // Verify all collection user/group refs are valid
        var groupNames = new HashSet<string>(roster.Groups.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var collection in roster.Collections)
        {
            if (collection.Groups is not null)
            {
                Assert.All(collection.Groups, cg => Assert.Contains(cg.Group, groupNames));
            }
            if (collection.Users is not null)
            {
                Assert.All(collection.Users, cu => Assert.Contains(cu.User, prefixSet));
            }
        }
    }
}
