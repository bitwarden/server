using Bit.Core.Entities;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Services;

[SutProviderCustomize]
public class AccessApprovalResolverTests
{
    [Theory, BitAutoData]
    public async Task ResolveAsync_NoReachableCollections_ReturnsNull(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByUserIdCipherIdAsync(userId, cipherId)
            .Returns(new List<CollectionCipher>());

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_CollectionWithoutAccessRule_ReturnsNull(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId, Collection collection)
    {
        collection.AccessRuleId = null;
        SetupReachableCollections(sutProvider, userId, cipherId, collection);

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_HumanApprovalRule_RequiresHumanApproval(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Rule = """{"kind":"human_approval"}""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.Equal(collection.Id, result.CollectionId);
        Assert.Equal(collection.OrganizationId, result.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_IpAllowlistRule_DoesNotRequireHumanApproval(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Rule = """{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_AllOfContainingHumanApproval_RequiresHumanApproval(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Rule = """{"kind":"all_of","rules":[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]},{"kind":"human_approval"}]}""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_MalformedRule_FailsSafeToHumanApproval(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Rule = "not json";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
    }

    private static void SetupReachableCollections(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId, params Collection[] collections)
    {
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByUserIdCipherIdAsync(userId, cipherId)
            .Returns(collections.Select(c => new CollectionCipher { CollectionId = c.Id, CipherId = cipherId }).ToList());
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections.ToList());
    }

    private static void SetupGovernedCollection(
        SutProvider<AccessApprovalResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        collection.AccessRuleId = rule.Id;
        SetupReachableCollections(sutProvider, userId, cipherId, collection);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(rule.Id)
            .Returns(rule);
    }
}
