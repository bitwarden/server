using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
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

    [Fact]
    public void Execute_Sampled_PinsCountsForSeed()
    {
        var context = NewPinFixtureContext();
        var step = new CreateDefaultCollectionsStep(PinFixtureOptions, DateTime.UtcNow.Date, seed: 1);

        step.Execute(context);

        var (active, trashed) = CountsPerMember(context);
        Assert.Equal(184, active.Sum());
        Assert.Equal(18, trashed.Sum());
        Assert.Equal(new[] { 50, 25, 21, 20, 15 }, active.OrderDescending().Take(5));
        Assert.Equal(new[] { 9, 6, 4 }, BucketCounts(active));
        Assert.Equal(
            new[] { 0, 1, 3, 5, 6, 8, 9, 15, 16, 18, 19, 21, 22, 23, 26, 29, 34, 36, 38, 39 },
            NonEmptyIndices(active));
    }

    [Fact]
    public void Execute_Quantile_TotalsAreIdenticalAcrossSeeds()
    {
        var runs = Enumerable.Range(1, 20).Select(seed => ExecutePinFixture(seed, DefaultCollectionSizing.Quantile)).ToList();

        Assert.All(runs.Skip(1), run => Assert.Equal(Totals(runs[0]), Totals(run)));
        Assert.NotEqual(NonEmptyIndices(runs[0].Active), NonEmptyIndices(runs[1].Active));
    }

    [Fact]
    public void Execute_Quantile_TrashIsApportionedExactly()
    {
        var (active, trashed) = ExecutePinFixture(seed: 1, DefaultCollectionSizing.Quantile);

        Assert.Equal((int)Math.Round(active.Sum() * 0.1m), trashed.Sum());
        Assert.All(Enumerable.Range(0, active.Length), u =>
            Assert.InRange(trashed[u], (int)Math.Floor(active[u] * 0.1m), (int)Math.Ceiling(active[u] * 0.1m)));
    }

    [Fact]
    public void Execute_TargetActiveItems_ReachesTargetAndKeepsLargest()
    {
        // The natural quantile total is far below 600, so most drawn sizes scale up and many hit the cap of 40
        var options = PinFixtureOptions with { TargetActiveItems = 600 };

        var (active, _) = ExecutePinFixture(seed: 1, DefaultCollectionSizing.Quantile, options);

        var sizes = active.OrderDescending().ToArray();
        Assert.Equal(600, active.Sum());
        Assert.Equal(50, sizes[0]);
        Assert.InRange(sizes[1], 1, 40);
        Assert.Equal(20, active.Count(a => a > 0));
    }

    [Theory]
    [InlineData(68)]
    [InlineData(811)]
    public void Execute_TargetActiveItems_OutOfRange_Throws(int target)
    {
        // largest 50 plus 19 drawn members at 1 to 40 items each reaches [69, 810]
        var context = NewPinFixtureContext();
        var options = PinFixtureOptions with { TargetActiveItems = target };
        var step = new CreateDefaultCollectionsStep(options, DateTime.UtcNow.Date, seed: 1);

        var ex = Assert.Throws<InvalidOperationException>(() => step.Execute(context));

        Assert.Contains($"'defaultCollections.targetActiveItems' {target} is outside the reachable range [69, 810]", ex.Message);
    }

    private static readonly SeedPresetDefaultCollections PinFixtureOptions = new()
    {
        NonEmptyRate = 0.5,
        Largest = [50],
        Sizes =
        [
            new SeedPresetSizeBucket { Min = 1, Max = 3, Weight = 0.5 },
            new SeedPresetSizeBucket { Min = 4, Max = 10, Weight = 0.3 },
            new SeedPresetSizeBucket { Min = 11, Max = 40, Weight = 0.2 },
        ],
        DeletedRate = 0.1,
    };

    private static int[] BucketCounts(int[] active) =>
        PinFixtureOptions.Sizes!.Select(b => active.Count(a => a >= b.Min && a <= b.Max)).ToArray();

    private static int[] NonEmptyIndices(int[] active) =>
        Enumerable.Range(0, active.Length).Where(u => active[u] > 0).ToArray();

    private static string Totals((int[] Active, int[] Trashed) run) =>
        $"active {run.Active.Sum()}, trashed {run.Trashed.Sum()}, non-empty {run.Active.Count(a => a > 0)}, " +
        $"top 5 [{string.Join(", ", run.Active.OrderDescending().Take(5))}], buckets [{string.Join(", ", BucketCounts(run.Active))}]";

    private static (int[] Active, int[] Trashed) ExecutePinFixture(
        int seed, DefaultCollectionSizing sizing, SeedPresetDefaultCollections? options = null)
    {
        var context = NewPinFixtureContext();
        new CreateDefaultCollectionsStep(options ?? PinFixtureOptions, DateTime.UtcNow.Date, seed, sizing: sizing).Execute(context);
        return CountsPerMember(context);
    }

    private static SeederContext NewPinFixtureContext()
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        InitGeneratorStep.FromDomain(TestDomain).Execute(context);
        for (var i = 0; i < 40; i++)
        {
            context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(Guid.NewGuid(), Guid.NewGuid(), "not-used-by-this-step"));
        }
        return context;
    }

    /// <summary>
    /// Active and trashed item counts per member, indexed like <see cref="EntityRegistry.UserDigests"/>.
    /// </summary>
    private static (int[] Active, int[] Trashed) CountsPerMember(SeederContext context)
    {
        var digests = context.Registry.UserDigests;
        var memberIndexByOrgUserId = digests.Select((d, u) => (d.OrgUserId, u)).ToDictionary(x => x.OrgUserId, x => x.u);
        var memberIndexByCollectionId = context.CollectionUsers
            .ToDictionary(cu => cu.CollectionId, cu => memberIndexByOrgUserId[cu.OrganizationUserId]);
        var trashedCipherIds = context.Ciphers.Where(c => c.DeletedDate is not null).Select(c => c.Id).ToHashSet();

        var active = new int[digests.Count];
        var trashed = new int[digests.Count];
        foreach (var link in context.CollectionCiphers)
        {
            var u = memberIndexByCollectionId[link.CollectionId];
            if (trashedCipherIds.Contains(link.CipherId))
            {
                trashed[u]++;
            }
            else
            {
                active[u]++;
            }
        }
        return (active, trashed);
    }
}
