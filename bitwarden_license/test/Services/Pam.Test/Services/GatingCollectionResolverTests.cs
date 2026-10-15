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
public class GatingCollectionResolverTests
{
    [Theory, BitAutoData]
    public async Task GetGatingCollectionIdsAsync_EnabledRule_ReturnsItsCollections(
        SutProvider<GatingCollectionResolver> sutProvider, Guid organizationId, Guid ruleId,
        Guid firstCollectionId, Guid secondCollectionId)
    {
        Arrange(sutProvider, organizationId,
            [new AccessRule { Id = ruleId, Enabled = true }],
            [new Collection { Id = firstCollectionId, AccessRuleId = ruleId },
             new Collection { Id = secondCollectionId, AccessRuleId = ruleId }]);

        var result = await sutProvider.Sut.GetGatingCollectionIdsAsync(organizationId);

        Assert.Equal(new HashSet<Guid> { firstCollectionId, secondCollectionId }, result);
    }

    [Theory, BitAutoData]
    public async Task GetGatingCollectionIdsAsync_ExcludesUngovernedAndDisabledRuleCollections(
        SutProvider<GatingCollectionResolver> sutProvider, Guid organizationId, Guid enabledRuleId,
        Guid disabledRuleId, Guid gatedCollectionId, Guid disabledCollectionId, Guid plainCollectionId)
    {
        Arrange(sutProvider, organizationId,
            [new AccessRule { Id = enabledRuleId, Enabled = true }, new AccessRule { Id = disabledRuleId, Enabled = false }],
            [new Collection { Id = gatedCollectionId, AccessRuleId = enabledRuleId },
             new Collection { Id = disabledCollectionId, AccessRuleId = disabledRuleId },
             new Collection { Id = plainCollectionId, AccessRuleId = null }]);

        var result = await sutProvider.Sut.GetGatingCollectionIdsAsync(organizationId);

        Assert.Equal(new HashSet<Guid> { gatedCollectionId }, result);
    }

    [Theory, BitAutoData]
    public async Task GetGatingCollectionIdsAsync_NoEnabledRules_ReturnsEmptyWithoutReadingCollections(
        SutProvider<GatingCollectionResolver> sutProvider, Guid organizationId, Guid ruleId)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(new List<AccessRule> { new() { Id = ruleId, Enabled = false } });

        var result = await sutProvider.Sut.GetGatingCollectionIdsAsync(organizationId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    private static void Arrange(SutProvider<GatingCollectionResolver> sutProvider, Guid organizationId,
        List<AccessRule> rules, List<Collection> collections)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(organizationId).Returns(rules);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organizationId).Returns(collections);
    }
}
