using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserGroups;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationUserGroupsAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(true, true)]
    public async Task ReadAllIds_UserCanManageUsersOrGroups_ShouldReturnSuccess(
        bool canManageUsers,
        bool canManageGroups,
        CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserGroupsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(contextOrganization.Id).Returns(canManageUsers);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(contextOrganization.Id).Returns(canManageGroups);

        var context = new AuthorizationHandlerContext(
            [OrganizationUserGroupOperations.ReadAllIds],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task ReadAllIds_UserCannotManageUsersNorGroups_ShouldReturnFailure(CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserGroupsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(contextOrganization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(contextOrganization.Id).Returns(false);

        var context = new AuthorizationHandlerContext(
            [OrganizationUserGroupOperations.ReadAllIds],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }
}
