using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Services;
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
        // An ungated path is an escape, since the caller can reach the cipher without a singleton rule.
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

    [Theory, BitAutoData]
    public async Task AppliesAsync_OneDisabledSingletonRulePath_ReturnsFalse(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId,
        Collection singletonCollection, Collection disabledCollection, AccessRule singletonRule,
        AccessRule disabledRule)
    {
        singletonRule.SingleActiveLease = true;
        disabledRule.SingleActiveLease = true;
        SetupGovernedCollection(sutProvider, singletonCollection, singletonRule);
        SetupGovernedCollection(sutProvider, disabledCollection, disabledRule);
        disabledRule.Enabled = false;
        SetupReachableCollections(sutProvider, userId, cipherId, singletonCollection, disabledCollection);

        // A disabled rule governs nothing, so its path is an escape like an ungated one.
        Assert.False(await sutProvider.Sut.AppliesAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task AppliesAsync_OnlyPathGovernedByDisabledSingletonRule_ReturnsFalse(
        SutProvider<SingleActiveLeaseEvaluator> sutProvider, Guid userId, Guid cipherId,
        Collection collection, AccessRule rule)
    {
        rule.SingleActiveLease = true;
        SetupGovernedCollection(sutProvider, collection, rule);
        rule.Enabled = false;
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
        // Pinned so AutoFixture's bool sequence does not decide whether the path governs.
        rule.Enabled = true;
        collection.AccessRuleId = rule.Id;
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);
    }
}
