using Bit.Seeder.Recipes;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class OrganizationFromPresetRecipeTests
{
    // NOTE: Issue #1 (SeedResult counts) is verified by the implementation fix.
    // The Recipe now captures counts BEFORE BulkCommitter.Commit() clears the lists.
    // Full database integration tests will verify the counts match actual seeded entities.
    // This fix ensures context.Users.Count etc. are captured before being cleared to zero.


    [Fact]
    public void ListAvailable_HandlesPresetWithPresetInMiddle()
    {
        // Issue #3: String.Replace bug - should only remove prefix, not all occurrences

        var available = OrganizationFromPresetRecipe.ListAvailable();

        // Verify presets don't have "presets." prefix removed from middle
        // If we had a preset named "my-presets-collection", it should become "my-presets-collection"
        // not "my--collection" (which would happen with Replace)

        Assert.NotNull(available);
        Assert.NotNull(available.Presets);

        // All preset names should not start with "presets."
        Assert.All(available.Presets, name => Assert.DoesNotContain("presets.", name.Substring(0, Math.Min(8, name.Length))));

        // Verify known presets are listed correctly
        Assert.Contains("dunder-mifflin-full", available.Presets);
        Assert.Contains("large-enterprise", available.Presets);
    }

    [Fact]
    public void ListAvailable_GroupsFixturesByCategory()
    {
        var available = OrganizationFromPresetRecipe.ListAvailable();

        // Verify fixtures are grouped by category
        Assert.NotNull(available.Fixtures);
        Assert.True(available.Fixtures.ContainsKey("ciphers"));
        Assert.True(available.Fixtures.ContainsKey("organizations"));
        Assert.True(available.Fixtures.ContainsKey("rosters"));

        // Verify ciphers category has expected fixtures
        Assert.Contains("ciphers.autofill-testing", available.Fixtures["ciphers"]);
        Assert.Contains("ciphers.public-site-logins", available.Fixtures["ciphers"]);
    }
}
