using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

[SutProviderCustomize]
public class GetPendingAutoConfirmUsersQueryTests
{
    [Theory, BitAutoData]
    public async Task GetPendingAutoConfirmUsersAsync_OrgAbilityIsNull_ReturnsEmpty(
        Guid organizationId,
        SutProvider<GetPendingAutoConfirmUsersQuery> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility?)null);

        var result = await sutProvider.Sut.GetPendingAutoConfirmUsersAsync(organizationId);

        Assert.Empty(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyPendingAutoConfirmAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetPendingAutoConfirmUsersAsync_UseAutomaticUserConfirmationDisabled_ReturnsEmpty(
        Guid organizationId,
        SutProvider<GetPendingAutoConfirmUsersQuery> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseAutomaticUserConfirmation = false });

        var result = await sutProvider.Sut.GetPendingAutoConfirmUsersAsync(organizationId);

        Assert.Empty(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyPendingAutoConfirmAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetPendingAutoConfirmUsersAsync_PolicyDisabled_ReturnsEmpty(
        Guid organizationId,
        [Policy(PolicyType.AutomaticUserConfirmation, false)] PolicyStatus disabledPolicy,
        SutProvider<GetPendingAutoConfirmUsersQuery> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseAutomaticUserConfirmation = true });

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organizationId, PolicyType.AutomaticUserConfirmation)
            .Returns(disabledPolicy);

        var result = await sutProvider.Sut.GetPendingAutoConfirmUsersAsync(organizationId);

        Assert.Empty(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyPendingAutoConfirmAsync(Arg.Any<Guid>());
    }

}
