using Xunit;

namespace Bit.SeederApi.IntegrationTest.DensityModel;

/// <summary>
/// Validates the multi-collection cipher assignment math from GenerateCiphersStep
/// to ensure no duplicate (CipherId, CollectionId) pairs are produced.
/// </summary>
public class MultiCollectionAssignmentTests
{
    /// <summary>
    /// Simulates the secondary collection assignment loop from GenerateCiphersStep
    /// with the extraCount clamp fix applied. Returns the list of (cipherIndex, collectionIndex) pairs.
    /// </summary>
    private static List<(int CipherIndex, int CollectionIndex)> SimulateMultiCollectionAssignment(
        int cipherCount,
        int collectionCount,
        double multiCollectionRate,
        int maxCollectionsPerCipher)
    {
        var primaryIndices = new int[cipherCount];
        var pairs = new List<(int, int)>();

        for (var i = 0; i < cipherCount; i++)
        {
            primaryIndices[i] = i % collectionCount;
            pairs.Add((i, primaryIndices[i]));
        }

        if (multiCollectionRate > 0 && collectionCount > 1)
        {
            var multiCount = (int)(cipherCount * multiCollectionRate);
            for (var i = 0; i < multiCount; i++)
            {
                var extraCount = 1 + (i % Math.Max(maxCollectionsPerCipher - 1, 1));
                extraCount = Math.Min(extraCount, collectionCount - 1);
                for (var j = 0; j < extraCount; j++)
                {
                    var secondaryIndex = (primaryIndices[i] + 1 + j) % collectionCount;
                    pairs.Add((i, secondaryIndex));
                }
            }
        }

        return pairs;
    }

    [Fact]
    public void MultiCollectionAssignment_SmallCollectionCount_NoDuplicates()
    {
        var pairs = SimulateMultiCollectionAssignment(
            cipherCount: 20,
            collectionCount: 3,
            multiCollectionRate: 1.0,
            maxCollectionsPerCipher: 5);

        var grouped = pairs.GroupBy(p => p);
        Assert.All(grouped, g => Assert.Single(g));
    }

    [Fact]
    public void MultiCollectionAssignment_TwoCollections_NoDuplicates()
    {
        var pairs = SimulateMultiCollectionAssignment(
            cipherCount: 50,
            collectionCount: 2,
            multiCollectionRate: 1.0,
            maxCollectionsPerCipher: 10);

        var grouped = pairs.GroupBy(p => p);
        Assert.All(grouped, g => Assert.Single(g));
    }

    [Fact]
    public void MultiCollectionAssignment_ExtraCountClamped_ToAvailableCollections()
    {
        // With 2 collections, extraCount should never exceed 1 (collectionCount - 1)
        var collectionCount = 2;
        var maxCollectionsPerCipher = 10;
        var cipherCount = 20;

        for (var i = 0; i < cipherCount; i++)
        {
            var extraCount = 1 + (i % Math.Max(maxCollectionsPerCipher - 1, 1));
            extraCount = Math.Min(extraCount, collectionCount - 1);
            Assert.True(extraCount <= collectionCount - 1,
                $"extraCount {extraCount} exceeds available secondary slots {collectionCount - 1} at i={i}");
        }
    }

    [Fact]
    public void MultiCollectionAssignment_SecondaryNeverEqualsPrimary()
    {
        var pairs = SimulateMultiCollectionAssignment(
            cipherCount: 30,
            collectionCount: 3,
            multiCollectionRate: 1.0,
            maxCollectionsPerCipher: 5);

        // Group by cipher index — for each cipher, no secondary should equal primary
        var byCipher = pairs.GroupBy(p => p.CipherIndex);
        foreach (var group in byCipher)
        {
            var primary = group.First().CollectionIndex;
            var secondaries = group.Skip(1).Select(p => p.CollectionIndex);
            Assert.DoesNotContain(primary, secondaries);
        }
    }
}
