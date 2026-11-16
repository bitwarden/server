using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class GenerateCiphersStepTests
{
    // Secure notes only: composing an SSH key cipher builds a pool of 500 RSA keys, which takes about 30 seconds
    private static readonly Distribution<CipherType> _notesOnly = new((CipherType.SecureNote, 1.0));

    [Fact]
    public void Execute_CollectionTargetSizes_FillsEachCollectionToItsTarget()
    {
        var context = ContextWithCollections([30, 10, 5, 1, 0], maxCollectionsPerCipher: 2);

        new GenerateCiphersStep(30, _notesOnly).Execute(context);

        Assert.Equal([30, 10, 5, 1, 0], RowsPerCollection(context));
    }

    [Fact]
    public void Execute_MaxCollectionsPerCipher_NoCipherExceedsCap()
    {
        var context = ContextWithCollections(Enumerable.Repeat(10, 10).ToList(), maxCollectionsPerCipher: 6);

        new GenerateCiphersStep(25, _notesOnly).Execute(context);

        Assert.All(context.CollectionCiphers.GroupBy(cc => cc.CipherId), rows => Assert.True(rows.Count() <= 6));
    }

    [Fact]
    public void Execute_OrphanCipherRate_LeavesThatShareOfCiphersInNoCollection()
    {
        var context = ContextWithCollections([20, 20, 20], maxCollectionsPerCipher: 2);

        new GenerateCiphersStep(50, _notesOnly, density: new DensityProfile { OrphanCipherRate = 0.2 }).Execute(context);

        // (int)(50 × 0.2) = 10
        var assigned = context.CollectionCiphers.Select(cc => cc.CipherId).ToHashSet();
        Assert.Equal(10, context.Ciphers.Count(c => !assigned.Contains(c.Id)));
    }

    [Fact]
    public void Execute_CollectionTargetSizes_NoDuplicateCipherCollectionRows()
    {
        // 10 leftover slots for 20 ciphers already in one of the two collections: about half of the picks for a
        // slot land on a cipher already in that collection
        var context = ContextWithCollections([15, 15], maxCollectionsPerCipher: 2);

        new GenerateCiphersStep(20, _notesOnly).Execute(context);

        Assert.Equal(30, context.CollectionCiphers.Select(cc => (cc.CipherId, cc.CollectionId)).Distinct().Count());
    }

    [Fact]
    public void Execute_FullDepartmentLocality_ReusesCiphersFromTheSameDepartmentForExtraSlots()
    {
        // Two departments of three collections. 36 ciphers fill 36 of the 60 slots as primaries; the other 24 reuse them
        var context = ContextWithCollections(Enumerable.Repeat(10, 6).ToList(), maxCollectionsPerCipher: 3);
        context.Registry.CollectionDepartments.AddRange([0, 0, 0, 1, 1, 1]);
        context.Registry.DepartmentLocality = 1.0;

        new GenerateCiphersStep(36, _notesOnly).Execute(context);

        var department = context.Registry.CollectionIds.Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => context.Registry.CollectionDepartments[x.i]);
        var multi = context.CollectionCiphers.GroupBy(cc => cc.CipherId).Where(rows => rows.Count() > 1).ToList();
        Assert.NotEmpty(multi);
        Assert.All(multi, rows => Assert.Single(rows.Select(cc => department[cc.CollectionId]).Distinct()));
    }

    [Fact]
    public void Execute_FewerSlotsThanNonOrphanCiphers_Throws()
    {
        var context = ContextWithCollections([2, 1], maxCollectionsPerCipher: 1);

        var ex = Assert.Throws<InvalidOperationException>(() => new GenerateCiphersStep(10, _notesOnly).Execute(context));

        Assert.Contains("Access shape has 3 collection slots but 10 non-orphan ciphers", ex.Message);
    }

    [Fact]
    public void Execute_NoTargetSizesAndNoDensity_AssignsRoundRobinWithEveryThirdCipherInTwoCollections()
    {
        var context = ContextWithCollections([], maxCollectionsPerCipher: 1, collectionCount: 4);

        new GenerateCiphersStep(9, _notesOnly).Execute(context);

        Assert.Equal(
            [(0, 0), (0, 1), (1, 1), (2, 2), (3, 3), (3, 0), (4, 0), (5, 1), (6, 2), (6, 3), (7, 3), (8, 0)],
            Rows(context));
    }

    [Fact]
    public void Execute_NoTargetSizesWithDensity_AssignsNonOrphansRoundRobin()
    {
        var context = ContextWithCollections([], maxCollectionsPerCipher: 1, collectionCount: 4);

        new GenerateCiphersStep(10, _notesOnly, density: new DensityProfile { OrphanCipherRate = 0.2 }).Execute(context);

        Assert.Equal([(0, 0), (1, 1), (2, 2), (3, 3), (4, 0), (5, 1), (6, 2), (7, 3)], Rows(context));
    }

    private static SeederContext ContextWithCollections(List<int> targetSizes, int maxCollectionsPerCipher, int? collectionCount = null)
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);
        InitGeneratorStep.FromDomain(TestDomain).Execute(context);
        context.Registry.CollectionIds.AddRange(Enumerable.Range(0, collectionCount ?? targetSizes.Count).Select(_ => Guid.NewGuid()));
        context.Registry.CollectionTargetSizes.AddRange(targetSizes);
        context.Registry.MaxCollectionsPerCipher = maxCollectionsPerCipher;
        return context;
    }

    private static List<int> RowsPerCollection(SeederContext context) =>
        context.Registry.CollectionIds.Select(id => context.CollectionCiphers.Count(cc => cc.CollectionId == id)).ToList();

    private static List<(int Cipher, int Collection)> Rows(SeederContext context)
    {
        var cipherIndex = context.Ciphers.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var collectionIndex = context.Registry.CollectionIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return context.CollectionCiphers.Select(cc => (cipherIndex[cc.CipherId], collectionIndex[cc.CollectionId])).ToList();
    }
}
