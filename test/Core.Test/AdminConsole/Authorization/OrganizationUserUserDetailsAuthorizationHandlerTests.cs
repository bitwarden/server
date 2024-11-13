using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationUserUserDetailsAuthorizationHandlerTests
{
    [Theory, CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task ReadAll_Admins_Success(
        OrganizationUserType userType,
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = userType;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        if (userType == OrganizationUserType.Custom)
        {
            organization.Permissions.ManageUsers = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationScope(organization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CurrentContextOrganizationCustomize]
    public async Task ReadAll_ProviderUser_Success(
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = OrganizationUserType.User;
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(organization.Id)
            .Returns(true);

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationScope(organization.Id));

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CurrentContextOrganizationCustomize]
    public async Task ReadAll_User_NoSuccess(
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider)
    {
        organization.Type = OrganizationUserType.User;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationScope(organization.Id)
        );

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task ReadAll_NotMember_NoSuccess(
        CurrentContextOrganization organization,
        SutProvider<OrganizationUserUserDetailsAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { OrganizationUserUserDetailsOperations.ReadAll },
            new ClaimsPrincipal(),
            new OrganizationScope(organization.Id)
        );

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }
}
