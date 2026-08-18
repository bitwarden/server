using Bit.Services.Pam.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class SingleActiveLeaseEvaluatorTests
{
    [Theory, BitAutoData]
    public async Task AppliesAsync_NoReachableCollections_ReturnsFalse(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByUserIdCipherIdAsync(userId, cipherId)
            .Returns(new List<CollectionCipher>());

        // No path at all: the constraint does not bind.
        Assert.False(await sutProvider.Sut.AppliesAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task AppliesAsync_EveryPathGovernedBySingletonRule_ReturnsTrue(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId,
        Collection collectionA, Collection collectionB, AccessRule ruleA, AccessRule ruleB)
    {
        ruleA.SingleActiveLease = true;
        ruleB.SingleActiveLease = true;
        SetupGovernedCollection(sutProvider, collectionA, ruleA);
        SetupGovernedCollection(sutProvider, collectionB, ruleB);
        SetupReachableCollections(sutProvider, userId, cipherId, collectionA, collectionB);

        Assert.True(await sutProvider.Sut.AppliesAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task AppliesAsync_OneUngatedPath_ReturnsFalse(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId,
        Collection singletonCollection, Collection ungatedCollection, AccessRule singletonRule)
    {
        singletonRule.SingleActiveLease = true;
        SetupGovernedCollection(sutProvider, singletonCollection, singletonRule);
        // An ungated path is an escape: the caller can reach the cipher without any singleton rule.
        ungatedCollection.AccessRuleId = null;
        SetupReachableCollections(sutProvider, userId, cipherId, singletonCollection, ungatedCollection);

        Assert.False(await sutProvider.Sut.AppliesAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task AppliesAsync_OneNonSingletonRulePath_ReturnsFalse(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId,
        Collection singletonCollection, Collection plainCollection, AccessRule singletonRule, AccessRule plainRule)
    {
        singletonRule.SingleActiveLease = true;
        plainRule.SingleActiveLease = false;
        SetupGovernedCollection(sutProvider, singletonCollection, singletonRule);
        SetupGovernedCollection(sutProvider, plainCollection, plainRule);
        SetupReachableCollections(sutProvider, userId, cipherId, singletonCollection, plainCollection);

        // A non-singleton rule on any path is an escape.
        Assert.False(await sutProvider.Sut.AppliesAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task AppliesAsync_MissingRuleOnPath_ReturnsFalse(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId, Collection collection, Guid ruleId)
    {
        collection.AccessRuleId = ruleId;
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(ruleId).Returns((AccessRule?)null);
        SetupReachableCollections(sutProvider, userId, cipherId, collection);

        Assert.False(await sutProvider.Sut.AppliesAsync(userId, cipherId));
    }

    private static void SetupReachableCollections(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId, params Collection[] collections)
    {
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByUserIdCipherIdAsync(userId, cipherId)
            .Returns(collections.Select(c => new CollectionCipher { CollectionId = c.Id, CipherId = cipherId }).ToList());
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections.ToList());
    }

    private static void SetupGovernedCollection(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Collection collection, AccessRule rule)
    {
        collection.AccessRuleId = rule.Id;
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);
    }
}
