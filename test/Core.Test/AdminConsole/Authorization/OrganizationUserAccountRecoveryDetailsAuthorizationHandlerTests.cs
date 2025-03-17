using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserAccountRecoveryDetails;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationUserAccountRecoveryDetailsAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData]
    public async Task ReadAll_UserCanManageResetPassword_ShouldReturnSuccess(
        CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserAccountRecoveryDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(contextOrganization.Id).Returns(true);

        var context = new AuthorizationHandlerContext(
            [OrganizationUsersAccountRecoveryDetailsOperations.ReadAll],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task ReadAll_UserCannotManageResetPassword_ShouldReturnFailure(
        CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserAccountRecoveryDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(contextOrganization.Id).Returns(false);

        var context = new AuthorizationHandlerContext(
            [OrganizationUsersAccountRecoveryDetailsOperations.ReadAll],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }

    [Theory]
    [BitAutoData]
    public async Task Read_UserCanManageResetPassword_ShouldReturnSuccess(
        CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserAccountRecoveryDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(contextOrganization.Id).Returns(true);

        var context = new AuthorizationHandlerContext(
            [OrganizationUsersAccountRecoveryDetailsOperations.Read],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Read_UserCannotManageResetPassword_ShouldReturnFailure(
        CurrentContextOrganization contextOrganization,
        SutProvider<OrganizationUserAccountRecoveryDetailsAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(contextOrganization.Id).Returns(false);

        var context = new AuthorizationHandlerContext(
            [OrganizationUsersAccountRecoveryDetailsOperations.Read],
            new ClaimsPrincipal(),
            new OrganizationScope(contextOrganization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
    }
}
