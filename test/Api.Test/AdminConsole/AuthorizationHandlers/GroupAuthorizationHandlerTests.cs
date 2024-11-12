using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.AuthorizationHandlers;

[SutProviderCustomize]
public class GroupAuthorizationHandlerTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAllAsync_WhenMemberOfOrg_Success(
        OrganizationUserType userType,
        OrganizationScope scope,
        Guid userId, SutProvider<GroupAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll },
            new ClaimsPrincipal(),
            scope);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(scope).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadAllAsync_WithProviderUser_Success(
        Guid userId,
        OrganizationScope scope,
        SutProvider<GroupAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll },
            new ClaimsPrincipal(),
            scope);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(scope)
            .Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task CanReadAllAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        OrganizationScope scope,
        SutProvider<GroupAuthorizationHandler> sutProvider)
    {

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll },
            new ClaimsPrincipal(),
            scope
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task HandleRequirementsAsync_GivenViewDetailsOperation_WhenUserIsAdminOwner_ThenShouldSucceed(OrganizationUserType userType,
        OrganizationScope scope,
        CurrentContextOrganization organization, SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            [GroupOperations.ReadAllDetails],
            new ClaimsPrincipal(),
            scope
        );

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(scope).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task HandleRequirementsAsync_GivenViewDetailsOperation_WhenUserIsNotOwnerOrAdmin_ThenShouldFail(OrganizationUserType userType,
        OrganizationScope scope,
        CurrentContextOrganization organization, SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            [GroupOperations.ReadAllDetails],
            new ClaimsPrincipal(),
            scope
        );

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(scope).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementsAsync_GivenViewDetailsOperation_WhenUserHasManageGroupPermission_ThenShouldSucceed(
        OrganizationScope scope,
        CurrentContextOrganization organization, SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            ManageGroups = true
        };

        var context = new AuthorizationHandlerContext(
            [GroupOperations.ReadAllDetails],
            new ClaimsPrincipal(),
            scope
        );

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(scope).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementsAsync_GivenViewDetailsOperation_WhenUserHasManageUserPermission_ThenShouldSucceed(
        OrganizationScope scope,
        CurrentContextOrganization organization, SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            ManageUsers = true
        };

        var context = new AuthorizationHandlerContext(
            [GroupOperations.ReadAllDetails],
            new ClaimsPrincipal(),
            scope
        );

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(scope).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementsAsync_GivenViewDetailsOperation_WhenUserIsStandardUserTypeWithoutElevatedPermissions_ThenShouldFail(
        OrganizationScope scope,
        CurrentContextOrganization organization, SutProvider<GroupAuthorizationHandler> sutProvider)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            [GroupOperations.ReadAllDetails],
            new ClaimsPrincipal(),
            scope
        );

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(scope).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(scope).Returns(false);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task HandleRequirementsAsync_GivenViewDetailsOperation_WhenIsProviderUser_ThenShouldSucceed(
        OrganizationScope scope,
        SutProvider<GroupAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { GroupOperations.ReadAll },
            new ClaimsPrincipal(),
            scope);

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(scope).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(scope).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
