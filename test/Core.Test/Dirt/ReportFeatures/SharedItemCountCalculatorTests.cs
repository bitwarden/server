using System.Diagnostics;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Core.Test.Dirt.ReportFeatures;

public class SharedItemCountCalculatorTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Calculate_NoAccessAndNoContent_ReturnsEmpty()
    {
        var result = SharedItemCountCalculator.Calculate([], []);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_NoAccess_ReturnsEmpty()
    {
        var collectionId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [],
            [new CollectionCipherLink(collectionId, Guid.NewGuid())]);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_NoContent_ReturnsEmpty()
    {
        var result = SharedItemCountCalculator.Calculate(
            [new MemberCollectionAccess(Guid.NewGuid(), Guid.NewGuid())],
            []);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_MemberWithoutCollectionAccess_IsOmitted()
    {
        var withAccess = Guid.NewGuid();
        var withoutAccess = Guid.NewGuid();
        var collectionId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [new MemberCollectionAccess(withAccess, collectionId)],
            [new CollectionCipherLink(collectionId, Guid.NewGuid())]);

        Assert.Equal(1, result[withAccess]);
        Assert.False(result.ContainsKey(withoutAccess));
    }

    [Fact]
    public void Calculate_CollectionWithoutCiphers_OmitsMembersWithOnlyThatCollection()
    {
        var emptyHanded = Guid.NewGuid();
        var stocked = Guid.NewGuid();
        var emptyCollectionId = Guid.NewGuid();
        var stockedCollectionId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [
                new MemberCollectionAccess(emptyHanded, emptyCollectionId),
                new MemberCollectionAccess(stocked, stockedCollectionId)
            ],
            [new CollectionCipherLink(stockedCollectionId, Guid.NewGuid())]);

        Assert.False(result.ContainsKey(emptyHanded));
        Assert.Equal(1, result[stocked]);
        Assert.Single(result);
    }

    [Fact]
    public void Calculate_CipherInTwoAccessibleCollections_CountsItOnce()
    {
        var memberId = Guid.NewGuid();
        var firstCollectionId = Guid.NewGuid();
        var secondCollectionId = Guid.NewGuid();
        var sharedCipherId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [
                new MemberCollectionAccess(memberId, firstCollectionId),
                new MemberCollectionAccess(memberId, secondCollectionId)
            ],
            [
                new CollectionCipherLink(firstCollectionId, sharedCipherId),
                new CollectionCipherLink(secondCollectionId, sharedCipherId)
            ]);

        Assert.Equal(1, result[memberId]);
    }

    [Fact]
    public void Calculate_CipherInAccessibleAndInaccessibleCollection_IsStillCounted()
    {
        var memberId = Guid.NewGuid();
        var accessibleCollectionId = Guid.NewGuid();
        var otherCollectionId = Guid.NewGuid();
        var sharedCipherId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [new MemberCollectionAccess(memberId, accessibleCollectionId)],
            [
                new CollectionCipherLink(accessibleCollectionId, sharedCipherId),
                new CollectionCipherLink(otherCollectionId, sharedCipherId),
                new CollectionCipherLink(otherCollectionId, Guid.NewGuid())
            ]);

        Assert.Equal(1, result[memberId]);
    }

    [Fact]
    public void Calculate_MembersWithIdenticalAccess_ShareOneCount()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var firstCollectionId = Guid.NewGuid();
        var secondCollectionId = Guid.NewGuid();
        var sharedCipherId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [
                new MemberCollectionAccess(first, firstCollectionId),
                new MemberCollectionAccess(first, secondCollectionId),
                // The same set in the opposite order, which has to normalize to the same signature.
                new MemberCollectionAccess(second, secondCollectionId),
                new MemberCollectionAccess(second, firstCollectionId)
            ],
            [
                new CollectionCipherLink(firstCollectionId, Guid.NewGuid()),
                new CollectionCipherLink(firstCollectionId, sharedCipherId),
                new CollectionCipherLink(secondCollectionId, sharedCipherId),
                new CollectionCipherLink(secondCollectionId, Guid.NewGuid())
            ]);

        Assert.Equal(3, result[first]);
        Assert.Equal(3, result[second]);
    }

    [Fact]
    public void Calculate_DuplicateAccessEdges_DoNotInflateTheCount()
    {
        var repeated = Guid.NewGuid();
        var single = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [
                new MemberCollectionAccess(repeated, collectionId),
                new MemberCollectionAccess(repeated, collectionId),
                new MemberCollectionAccess(single, collectionId)
            ],
            [new CollectionCipherLink(collectionId, cipherId)]);

        Assert.Equal(1, result[repeated]);
        Assert.Equal(1, result[single]);
    }

    [Fact]
    public void Calculate_SubsetAccess_CountsOnlyReachableCiphers()
    {
        var broad = Guid.NewGuid();
        var narrow = Guid.NewGuid();
        var firstCollectionId = Guid.NewGuid();
        var secondCollectionId = Guid.NewGuid();
        var thirdCollectionId = Guid.NewGuid();
        var sharedCipherId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [
                new MemberCollectionAccess(broad, firstCollectionId),
                new MemberCollectionAccess(broad, secondCollectionId),
                new MemberCollectionAccess(broad, thirdCollectionId),
                new MemberCollectionAccess(narrow, firstCollectionId)
            ],
            [
                new CollectionCipherLink(firstCollectionId, sharedCipherId),
                new CollectionCipherLink(firstCollectionId, Guid.NewGuid()),
                new CollectionCipherLink(secondCollectionId, sharedCipherId),
                new CollectionCipherLink(secondCollectionId, Guid.NewGuid()),
                new CollectionCipherLink(thirdCollectionId, Guid.NewGuid())
            ]);

        Assert.Equal(4, result[broad]);
        Assert.Equal(2, result[narrow]);
    }

    [Fact]
    public void Calculate_UnreachableCollectionsAndCiphers_AreExcluded()
    {
        var memberId = Guid.NewGuid();
        var reachableCollectionId = Guid.NewGuid();
        var unreachableCollectionId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();

        var result = SharedItemCountCalculator.Calculate(
            [
                new MemberCollectionAccess(memberId, reachableCollectionId),
                new MemberCollectionAccess(otherMemberId, unreachableCollectionId)
            ],
            [
                new CollectionCipherLink(reachableCollectionId, Guid.NewGuid()),
                new CollectionCipherLink(unreachableCollectionId, Guid.NewGuid()),
                new CollectionCipherLink(unreachableCollectionId, Guid.NewGuid()),
                new CollectionCipherLink(Guid.NewGuid(), Guid.NewGuid())
            ]);

        Assert.Equal(1, result[memberId]);
        Assert.Equal(2, result[otherMemberId]);
        Assert.Equal(2, result.Count);
    }

    /// <summary>
    /// The shape that made the stored procedure unusable: every member sees every cipher, so a per-member
    /// walk of the ciphers would be 17,001 x 50,000 touches.
    /// </summary>
    [Fact]
    public void Calculate_UniversalAccessOrganization_CountsEveryMemberQuickly()
    {
        const int memberCount = 17_001;
        const int cipherCount = 50_000;

        var collectionId = Guid.NewGuid();
        var access = new List<MemberCollectionAccess>(memberCount);
        var memberIds = new List<Guid>(memberCount);
        for (var i = 0; i < memberCount; i++)
        {
            var memberId = Guid.NewGuid();
            memberIds.Add(memberId);
            access.Add(new MemberCollectionAccess(memberId, collectionId));
        }

        var content = new List<CollectionCipherLink>(cipherCount);
        for (var i = 0; i < cipherCount; i++)
        {
            content.Add(new CollectionCipherLink(collectionId, Guid.NewGuid()));
        }

        var stopwatch = Stopwatch.StartNew();
        var result = SharedItemCountCalculator.Calculate(access, content);
        stopwatch.Stop();

        testOutputHelper.WriteLine(
            $"{memberCount} members x {cipherCount} ciphers in {stopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(memberCount, result.Count);
        foreach (var memberId in memberIds)
        {
            Assert.Equal(cipherCount, result[memberId]);
        }
    }

    /// <summary>
    /// Many collections, overlapping access sets and ciphers that several collections share, so neither the
    /// access-set memoization nor the single-collection shortcut can carry the result on its own.
    /// </summary>
    [Fact]
    public void Calculate_ManyOverlappingAccessSets_CountsEveryMemberQuickly()
    {
        const int collectionCount = 2_000;
        const int ciphersPerCollection = 100;
        const int globalCipherCount = 10;
        const int collectionsPerMember = 100;
        const int memberCount = 10_000;
        const int stride = 7;

        var collectionIds = new Guid[collectionCount];
        for (var i = 0; i < collectionCount; i++)
        {
            collectionIds[i] = Guid.NewGuid();
        }

        var content = new List<CollectionCipherLink>(
            (collectionCount * ciphersPerCollection) + (collectionCount * globalCipherCount));
        for (var i = 0; i < collectionCount; i++)
        {
            for (var j = 0; j < ciphersPerCollection; j++)
            {
                content.Add(new CollectionCipherLink(collectionIds[i], Guid.NewGuid()));
            }
        }

        for (var i = 0; i < globalCipherCount; i++)
        {
            var globalCipherId = Guid.NewGuid();
            for (var j = 0; j < collectionCount; j++)
            {
                content.Add(new CollectionCipherLink(collectionIds[j], globalCipherId));
            }
        }

        var memberIds = new Guid[memberCount];
        var access = new List<MemberCollectionAccess>(memberCount * collectionsPerMember);
        for (var i = 0; i < memberCount; i++)
        {
            memberIds[i] = Guid.NewGuid();
            for (var j = 0; j < collectionsPerMember; j++)
            {
                var collection = (i + (j * stride)) % collectionCount;
                access.Add(new MemberCollectionAccess(memberIds[i], collectionIds[collection]));
            }
        }

        var stopwatch = Stopwatch.StartNew();
        var result = SharedItemCountCalculator.Calculate(access, content);
        stopwatch.Stop();

        testOutputHelper.WriteLine(
            $"{memberCount} members x {collectionsPerMember} collections over {content.Count} links " +
            $"in {stopwatch.ElapsedMilliseconds} ms");

        // The strided collections are distinct because the stride is coprime with the collection count, so
        // every member reaches the same number of ciphers.
        var expected = (collectionsPerMember * ciphersPerCollection) + globalCipherCount;
        Assert.Equal(memberCount, result.Count);
        foreach (var memberId in memberIds)
        {
            Assert.Equal(expected, result[memberId]);
        }
    }
}
