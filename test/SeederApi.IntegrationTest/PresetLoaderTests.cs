using Bit.Seeder;
using Bit.Seeder.Pipeline;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class PresetLoaderTests
{
    [Fact]
    public void Load_FixtureOrgWithGeneratedCiphers_InitializesGenerator()
    {
        // Issue #2: Fixture-based org + generated ciphers should resolve domain from fixture
        // This test verifies that when a preset uses a fixture org (no explicit domain)
        // but wants to generate ciphers (needs domain for generator), the domain is
        // automatically resolved by reading the org fixture.

        var services = new ServiceCollection();
        var builder = services.AddRecipe("fixture-org-test");

        builder
            .UseOrganization("dunder-mifflin")  // Fixture org (domain in fixture)
            .AddOwner()
            .WithGenerator("dundermifflin.com")  // Generator needs domain
            .AddCiphers(50);

        // This should NOT throw "Generated ciphers require a generator"
        builder.Validate();

        using var provider = services.BuildServiceProvider();
        var steps = provider.GetKeyedServices<IStep>("fixture-org-test").ToList();

        Assert.NotNull(steps);
        Assert.NotEmpty(steps);
    }
}
