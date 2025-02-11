using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

[SutProviderCustomize]
public class InviteOrganizationUsersAuthorizationHandlerTests
{

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_WhenCurrentUserIsOwner_ThenWouldBeAllowedToInviteUsers(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.User, new Permissions(),
            string.Empty, false);

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenCurrentUserIsNotAnOwner_WhenInvitingAnOwner_ThenNotAllowedToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Owner, new Permissions(),
            string.Empty, false);

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.OwnerCanOnlyConfigureAnotherOwnersAccount));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenCurrentUserIsAdmin_WhenInvitingNonOwner_ThenWouldBeAllowedToInviteUsers(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Admin, new Permissions(),
            string.Empty, false);

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenCurrentUserCannotManageUsers_ThenNotAllowedToInviteUsers(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.User, new Permissions(),
            string.Empty, false);

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.DoesNotHavePermissionToMangeUsers));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenCurrentUserCanManageUsers_WhenInvitingAnAdmin_ThenNotAllowedToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Admin, new Permissions(),
            string.Empty, false);

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersCannotManageAdminsOrOwners));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenCurrentUserCanManageUsers_WhenOrganizationDoesNotHaveCustomPermissions_ThenNotAllowedToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, new Permissions(),
            string.Empty, false);

        organization.UseCustomPermissions = false;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.EnableCustomPermissionsOrganizationMustBeEnterprise));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenCurrentUserCanManageUsers_WhenNoPermissionsAreProvided_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, null,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotAccessReports_WhenInvitedUserCanAccessReports_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            AccessReports = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessReports(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotManageGroups_WhenInvitedUserCanManageGroups_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            ManageGroups = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotManagePolicies_WhenInvitedUserCanManagePolicies_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            ManagePolicies = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManagePolicies(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotManageScim_WhenInvitedUserCanManageScim_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            ManageScim = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageScim(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotManageSso_WhenInvitedUserCanManageSso_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            ManageSso = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageSso(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }
    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotAccessEventLogs_WhenInvitedUserCanAccessEventLogs_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            AccessEventLogs = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessEventLogs(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotAccessImportExport_WhenInvitedUserCanAccessImportExport_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            AccessImportExport = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessImportExport(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotEditAnyCollection_WhenInvitedUserCanEditAnyCollection_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            EditAnyCollection = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().EditAnyCollection(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenUserCannotManageResetPassword_WhenInvitedUserCanManageResetPassword_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            ManageResetPassword = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organization.Id).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenOrganizationPermissionCannotCreateNewCollections_WhenInvitedUserCanCreateNewCollections_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            CreateNewCollections = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(new CurrentContextOrganization
        {
            Permissions = new Permissions
            {
                CreateNewCollections = false
            }
        });


        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_GivenOrganizationPermissionCannotDeleteAnyCollection_WhenInvitedUserCanDeleteAnyCollection_ThenShouldNotBeAbleToInvite(Organization organization, DateTime performedAt, Guid performedBy, SutProvider<InviteOrganizationUsersAuthorizationHandler> sutProvider)
    {
        var permissions = new Permissions
        {
            DeleteAnyCollection = true
        };

        var invite = OrganizationUserInvite.Create(["test@email.com"], [], OrganizationUserType.Custom, permissions,
            string.Empty, false);

        organization.UseCustomPermissions = true;

        var request = new InviteOrganizationUsersRequest([invite], OrganizationDto.FromOrganization(organization), performedBy, performedAt);

        var context = new AuthorizationHandlerContext(
            [InviteOrganizationUserOperations.Invite],
            new ClaimsPrincipal(),
            request
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(performedBy);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(new CurrentContextOrganization
        {
            Permissions = new Permissions
            {
                DeleteAnyCollection = false
            }
        });


        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.True(context.FailureReasons.All(x =>
            x.Message == InviteOrganizationUsersAuthorizationHandler.CustomUsersOnlyGrantSamePermissions));
    }
}
