using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUsersResetPasswordDetails;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationUserResetPasswordDetailsAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData]
    public async Task Read_UserCanManageResetPassword_ShouldReturnSuccess(
        CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserResetPasswordDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(contextOrganization.Id).Returns(true);

        var context = new AuthorizationHandlerContext(
            [OrganizationUsersResetPasswordDetailsOperations.Read],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Read_UserCannotManageResetPassword_ShouldReturnFailure(
        CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserResetPasswordDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(contextOrganization.Id).Returns(false);

        var context = new AuthorizationHandlerContext(
            [OrganizationUsersResetPasswordDetailsOperations.Read],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }
}
