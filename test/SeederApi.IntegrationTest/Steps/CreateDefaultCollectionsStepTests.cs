using Bit.Seeder.Data.Distributions;
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

    [Fact]
    public void Execute_SizeWeightsTotalZero_Throws()
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        InitGeneratorStep.FromDomain(TestDomain).Execute(context);
        for (var i = 0; i < 4; i++)
        {
            context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.NewGuid(), "not-used-by-this-step"));
        }
        var options = new SeedPresetDefaultCollections
        {
            NonEmptyRate = 1.0,
            Sizes = [new SeedPresetSizeBucket { Min = 1, Max = 5, Weight = 0 }],
        };
        var step = new CreateDefaultCollectionsStep(options, DateTime.UtcNow.Date, seed: 1);

        var ex = Assert.Throws<ArgumentException>(() => step.Execute(context));

        Assert.Contains("positive total weight", ex.Message);
    }

    [Fact]
    public void Execute_MembersWithPendingPersonalItems_KeepEmptyCollections()
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        InitGeneratorStep.FromDomain(TestDomain).Execute(context);
        for (var i = 0; i < 20; i++)
        {
            context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.NewGuid(), "not-used-by-this-step"));
        }
        // Digests 15-19 land in the second bucket and hold one pending personal item each.
        var pending = new Distribution<(int Min, int Max)>(((0, 0), 0.75), ((1, 1), 0.25));
        var options = new SeedPresetDefaultCollections { NonEmptyRate = 0.5 };
        var step = new CreateDefaultCollectionsStep(options, DateTime.UtcNow.Date, seed: 1, personalDist: pending);

        step.Execute(context);

        var pendingOrgUserIds = context.Registry.UserDigests.Skip(15).Select(d => d.OrgUserId).ToHashSet();
        var pendingCollectionIds = context.CollectionUsers
            .Where(cu => pendingOrgUserIds.Contains(cu.OrganizationUserId))
            .Select(cu => cu.CollectionId)
            .ToHashSet();
        Assert.NotEmpty(context.CollectionCiphers);
        Assert.DoesNotContain(context.CollectionCiphers, cc => pendingCollectionIds.Contains(cc.CollectionId));
    }
}
