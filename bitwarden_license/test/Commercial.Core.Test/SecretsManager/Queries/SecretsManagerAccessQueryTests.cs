using Bit.Commercial.Core.SecretsManager.Queries;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Models.Data.Organizations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries;

[SutProviderCustomize]
public class SecretsManagerAccessQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task HasAccessAsync_NoClaim_ReturnsFalseWithoutReadingAbility(
        SutProvider<SecretsManagerAccessQuery> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(false);

        var result = await sutProvider.Sut.HasAccessAsync(organizationId);

        Assert.False(result);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>().DidNotReceiveWithAnyArgs()
            .GetOrganizationAbilityAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessAsync_AbilityNotFound_ReturnsFalse(
        SutProvider<SecretsManagerAccessQuery> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility?)null);

        var result = await sutProvider.Sut.HasAccessAsync(organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    [BitAutoData(false, false)]
    public async Task HasAccessAsync_OrganizationNoLongerEligible_ReturnsFalse(
        bool useSecretsManager, bool enabled,
        SutProvider<SecretsManagerAccessQuery> sutProvider, OrganizationAbility ability)
    {
        ability.UseSecretsManager = useSecretsManager;
        ability.Enabled = enabled;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(ability.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(ability.Id)
            .Returns(ability);

        var result = await sutProvider.Sut.HasAccessAsync(ability.Id);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessAsync_ClaimAndOrganizationEligible_ReturnsTrue(
        SutProvider<SecretsManagerAccessQuery> sutProvider, OrganizationAbility ability)
    {
        ability.UseSecretsManager = true;
        ability.Enabled = true;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(ability.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(ability.Id)
            .Returns(ability);

        var result = await sutProvider.Sut.HasAccessAsync(ability.Id);

        Assert.True(result);
    }
}
