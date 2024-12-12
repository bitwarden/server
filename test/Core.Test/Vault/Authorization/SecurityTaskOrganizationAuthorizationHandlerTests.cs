using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization;

[SutProviderCustomize]
public class SecurityTaskOrganizationAuthorizationHandlerTests
{
    [Theory, CurrentContextOrganizationCustomize, BitAutoData]
    public async Task MissingOrg_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskOrganizationAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns((CurrentContextOrganization)null);

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.ListAllForOrganization },
            new ClaimsPrincipal(),
            organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize, BitAutoData]
    public async Task MissingUserId_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskOrganizationAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(null as Guid?);

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.ListAllForOrganization },
            new ClaimsPrincipal(),
            organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task ListAllForOrganization_Admin_Success(
        OrganizationUserType userType,
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskOrganizationAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        organization.Type = userType;
        if (organization.Type == OrganizationUserType.Custom)
        {
            organization.Permissions.AccessReports = true;
        }
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.ListAllForOrganization },
            new ClaimsPrincipal(),
            organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.User), BitAutoData]
    public async Task ListAllForOrganization_User_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskOrganizationAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.ListAllForOrganization },
            new ClaimsPrincipal(),
            organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

}
