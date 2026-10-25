using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreateDefaultCollectionsStepTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Execute_LargestHasMoreEntriesThanNonEmptyMembers_Throws(double nonEmptyRate)
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        InitGeneratorStep.FromDomain(TestDomain).Execute(context);
        for (var i = 0; i < 4; i++)
        {
            context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.NewGuid(), "not-used-by-this-step"));
        }
        var options = new SeedPresetDefaultCollections { NonEmptyRate = nonEmptyRate, Largest = [5, 4, 3] };
        var step = new CreateDefaultCollectionsStep(options, DateTime.UtcNow.Date, seed: 1);

        var ex = Assert.Throws<InvalidOperationException>(() => step.Execute(context));

        Assert.Contains("'defaultCollections.largest' has 3 entries but only", ex.Message);
    }
}
