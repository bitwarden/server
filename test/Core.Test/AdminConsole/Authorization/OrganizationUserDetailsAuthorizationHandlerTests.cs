using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserDetails;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationUserDetailsAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData]
    public async Task Read_UserCanManageUsers_ShouldReturnSuccess(CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(contextOrganization.Id).Returns(true);

        var context = new AuthorizationHandlerContext(
            [OrganizationUserDetailsOperations.Read],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Read_UserCannotManageUsers_ShouldReturnFailure(CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(contextOrganization.Id).Returns(false);

        var context = new AuthorizationHandlerContext(
            [OrganizationUserDetailsOperations.Read],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }
}
