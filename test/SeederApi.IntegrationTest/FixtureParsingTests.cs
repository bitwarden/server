using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

/// <summary>
/// Parses the encryption-mode fixtures/presets through the real <see cref="SeedReader"/> so a malformed
/// fixture — or an attachment referencing a body file that isn't embedded — fails in CI rather than only
/// surfacing at manual-seed time. Full seed-and-commit is exercised by the manual runbook.
/// </summary>
public sealed class FixtureParsingTests
{
    private const int _expectedCipherCount = 34;
    private readonly SeedReader _reader = new();

    [Fact]
    public void IndividualEncryptionModesPreset_ParsesWithExpectedCiphers()
    {
        var preset = _reader.Read<SeedPreset>("presets.individual.encryption-modes");

        Assert.True(preset.IsIndividual);
        Assert.Equal("encryption-modes", preset.Ciphers?.Fixture);

        var ciphers = _reader.Read<SeedFile>("ciphers.encryption-modes");
        Assert.Equal(_expectedCipherCount, ciphers.Items.Count);
    }

    [Fact]
    public void QaPaperTrailPartnersTeamPreset_ParsesWithOrgRosterAndCiphers()
    {
        var preset = _reader.Read<SeedPreset>("presets.qa.paper-trail-partners-team");

        Assert.False(preset.IsIndividual);
        Assert.Equal("paper-trail-partners", preset.Organization?.Fixture);
        Assert.Equal("teams-annually", preset.Organization?.PlanType);
        Assert.Equal("paper-trail-partners", preset.Roster?.Fixture);
        Assert.Equal("encryption-modes", preset.Ciphers?.Fixture);

        var org = _reader.Read<SeedOrganization>("organizations.paper-trail-partners");
        Assert.Equal("Paper Trail Partners", org.Name);

        var roster = _reader.Read<SeedRoster>("rosters.paper-trail-partners");
        Assert.Contains(roster.Users, u => u.Role == "owner");

        var ciphers = _reader.Read<SeedFile>("ciphers.encryption-modes");
        Assert.Equal(_expectedCipherCount, ciphers.Items.Count);
        Assert.Contains(ciphers.Items, i => i.Archived is true);
        Assert.Contains(ciphers.Items, i => i.Deleted is true);
    }

    [Fact]
    public void EncryptionModesFixture_CoversAllCipherTypesAndBothEncryptionModes()
    {
        var ciphers = _reader.Read<SeedFile>("ciphers.encryption-modes");

        // All eight cipher types are represented, including the newer bankAccount/driversLicense/passport.
        var types = ciphers.Items.Select(i => i.Type).ToHashSet();
        Assert.Superset(
            new HashSet<string> { "login", "card", "identity", "secureNote", "sshKey", "bankAccount", "driversLicense", "passport" },
            types);

        // Both cipher-encryption modes are exercised — cipherKey explicitly, userKey by omission (schema default).
        Assert.Contains(ciphers.Items, i => i.CipherEncryption == "cipherKey");
        Assert.Contains(ciphers.Items, i => i.CipherEncryption is null or "userKey");
    }

    [Fact]
    public void EnterpriseBasicFixture_CoversAllCipherTypesWithLifecycle()
    {
        var ciphers = _reader.Read<SeedFile>("ciphers.enterprise-basic");

        // The enrichment added the five types that were missing; assert all eight are present.
        var types = ciphers.Items.Select(i => i.Type).ToHashSet();
        Assert.Superset(
            new HashSet<string> { "login", "card", "identity", "secureNote", "sshKey", "bankAccount", "driversLicense", "passport" },
            types);

        Assert.Contains(ciphers.Items, i => i.Archived is true);
        Assert.Contains(ciphers.Items, i => i.Deleted == true);
    }

    [Theory]
    [InlineData("encryption-modes")]
    [InlineData("enterprise-basic")]
    public void AttachmentFixtures_ReferenceEmbeddedBodies(string fixture)
    {
        var ciphers = _reader.Read<SeedFile>($"ciphers.{fixture}");

        foreach (var item in ciphers.Items)
        {
            foreach (var attachment in item.Attachments ?? [])
            {
                Assert.NotEmpty(_reader.ReadBytes(attachment.File));
            }
        }
    }
}
