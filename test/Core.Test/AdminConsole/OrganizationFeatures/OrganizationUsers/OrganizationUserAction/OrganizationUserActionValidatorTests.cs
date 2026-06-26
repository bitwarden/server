using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

public class OrganizationUserActionValidatorTests
{
    private static OrganizationUserActionRequest BuildRequest(
        OrganizationUserType? actingRole,
        OrganizationUserType targetRole,
        bool hasManagePermission = false,
        bool isProvider = false)
    {
        // A null acting role represents a non-member (e.g. a provider-only user).
        var permissions = actingRole is null
            ? null
            : new Permissions { ManageUsers = hasManagePermission };

        return new OrganizationUserActionRequest(
            actingRole,
            permissions,
            p => p.ManageUsers,
            () => Task.FromResult(isProvider),
            targetRole);
    }

    private static OrganizationUserManageMembersRequest BuildManageMembersRequest(
        OrganizationUserType? actingRole,
        bool hasManagePermission = false,
        bool isProvider = false)
    {
        // A null acting role represents a non-member (e.g. a provider-only user).
        var permissions = actingRole is null
            ? null
            : new Permissions { ManageUsers = hasManagePermission };

        return new OrganizationUserManageMembersRequest(
            actingRole,
            permissions,
            p => p.ManageUsers,
            () => Task.FromResult(isProvider));
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner, false, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Owner, false, OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner, false, OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Owner, false, OrganizationUserType.Custom)]
    [InlineData(OrganizationUserType.Admin, false, OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Admin, false, OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Admin, false, OrganizationUserType.Custom)]
    [InlineData(OrganizationUserType.Custom, true, OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom, true, OrganizationUserType.Custom)]
    public async Task ValidateAsync_WhenActingUserCanManageTargetRole_ReturnsSuccess(
        OrganizationUserType actingRole,
        bool actingUserHasManagePermission,
        OrganizationUserType targetRole)
    {
        // Note: Owners and Admins manage by role, so the manage permission is irrelevant for them.
        var request = BuildRequest(actingRole, targetRole, hasManagePermission: actingUserHasManagePermission);

        var result = await OrganizationUserActionValidator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin, true, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, true, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, true, OrganizationUserType.Admin)]
    public async Task ValidateAsync_WhenTargetRoleOutranksActingUser_ReturnsCannotManageHigherRoleError(
        OrganizationUserType actingRole,
        bool actingUserHasManagePermission,
        OrganizationUserType targetRole)
    {
        var request = BuildRequest(actingRole, targetRole, hasManagePermission: actingUserHasManagePermission);

        var result = await OrganizationUserActionValidator.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotManageHigherRoleError>(result.AsError);
    }

    [Theory]
    // A regular User cannot manage any member, regardless of target role.
    [InlineData(OrganizationUserType.User, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.User, OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User, OrganizationUserType.User)]
    [InlineData(OrganizationUserType.User, OrganizationUserType.Custom)]
    public async Task ValidateAsync_WhenActingUserIsRegularUser_ReturnsMissingManagePermissionError(
        OrganizationUserType actingRole,
        OrganizationUserType targetRole)
    {
        var request = BuildRequest(actingRole, targetRole);

        var result = await OrganizationUserActionValidator.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<MissingManagePermissionError>(result.AsError);
    }

    [Theory]
    // A Custom user without the manage permission has no authority over any member.
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task ValidateAsync_WhenCustomUserLacksManagePermission_ReturnsMissingManagePermissionError(
        OrganizationUserType targetRole)
    {
        var request = BuildRequest(OrganizationUserType.Custom, targetRole, hasManagePermission: false);

        var result = await OrganizationUserActionValidator.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<MissingManagePermissionError>(result.AsError);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task ValidateAsync_WhenActingUserIsProvider_ReturnsSuccessForAnyTargetRole(
        OrganizationUserType targetRole)
    {
        // A provider user has Owner-level authority. They are not a member of the organization, so their
        // role/permissions are null and authority comes solely from the provider callback.
        var request = BuildRequest(actingRole: null, targetRole, isProvider: true);

        var result = await OrganizationUserActionValidator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAuthorizedByRole_DoesNotInvokeProviderCallback()
    {
        // The provider check hits the database, so it must not be invoked when the role/permission checks
        // already authorize the action.
        var providerCallbackInvoked = false;
        var request = new OrganizationUserActionRequest(
            OrganizationUserType.Owner,
            new Permissions(),
            p => p.ManageUsers,
            () =>
            {
                providerCallbackInvoked = true;
                return Task.FromResult(false);
            },
            OrganizationUserType.User);

        var result = await OrganizationUserActionValidator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.False(providerCallbackInvoked);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public async Task ValidateCanManageMembersAsync_WhenOwnerOrAdmin_ReturnsSuccess(
        OrganizationUserType actingRole)
    {
        var request = BuildManageMembersRequest(actingRole);

        var result = await OrganizationUserActionValidator.ValidateCanManageMembersAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateCanManageMembersAsync_WhenCustomUser_DependsOnManagePermission(
        bool hasManagePermission)
    {
        var request = BuildManageMembersRequest(OrganizationUserType.Custom, hasManagePermission);

        var result = await OrganizationUserActionValidator.ValidateCanManageMembersAsync(request);

        Assert.Equal(hasManagePermission, result.IsValid);
    }

    [Fact]
    public async Task ValidateCanManageMembersAsync_WhenProvider_ReturnsSuccess()
    {
        var request = BuildManageMembersRequest(actingRole: null, isProvider: true);

        var result = await OrganizationUserActionValidator.ValidateCanManageMembersAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateCanManageMembersAsync_WhenRegularUser_ReturnsMissingManagePermissionError()
    {
        var request = BuildManageMembersRequest(OrganizationUserType.User);

        var result = await OrganizationUserActionValidator.ValidateCanManageMembersAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<MissingManagePermissionError>(result.AsError);
    }
}
