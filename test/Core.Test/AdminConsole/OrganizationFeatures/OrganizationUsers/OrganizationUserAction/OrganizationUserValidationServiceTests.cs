using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

public class OrganizationUserValidationServiceTests
{
    private static readonly OrganizationUserValidationService _sut = new();

    // A null acting user represents a non-member (e.g. a provider-only user). Custom users are granted the
    // ManageUsers permission by default, since that is the authority a Custom user needs to act on members.
    private static OrganizationUser? ActingUser(OrganizationUserType? role, bool manageUsers = true)
    {
        if (role is null)
        {
            return null;
        }

        var actingUser = new OrganizationUser { Type = role.Value };
        if (role is OrganizationUserType.Custom)
        {
            actingUser.SetPermissions(new Permissions { ManageUsers = manageUsers });
        }

        return actingUser;
    }

    private static OrganizationUser TargetUser(OrganizationUserType role) =>
        new() { Type = role };

    [Theory]
    [InlineData(OrganizationUserType.Owner, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Owner, OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner, OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Owner, OrganizationUserType.Custom)]
    [InlineData(OrganizationUserType.Admin, OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Admin, OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Admin, OrganizationUserType.Custom)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.Custom)]
    public void CanManage_WhenTargetRoleWithinAuthority_ReturnsNull(
        OrganizationUserType actingRole,
        OrganizationUserType targetRole)
    {
        var result = _sut.CanManage(ActingUser(actingRole), TargetUser(targetRole), actingUserIsProvider: false);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.Admin)]
    public void CanManage_WhenTargetRoleOutranksActingUser_ReturnsCannotManageTargetUser(
        OrganizationUserType actingRole,
        OrganizationUserType targetRole)
    {
        var result = _sut.CanManage(ActingUser(actingRole), TargetUser(targetRole), actingUserIsProvider: false);

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Theory]
    // A regular User has no management authority over any role.
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanManage_WhenActingUserIsRegularUser_ReturnsCannotManageTargetUser(
        OrganizationUserType targetRole)
    {
        var result = _sut.CanManage(ActingUser(OrganizationUserType.User), TargetUser(targetRole), actingUserIsProvider: false);

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Theory]
    // A provider user has Owner-level authority and is not an organization member, so their role is null and
    // authority comes solely from the provider flag.
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanManage_WhenActingUserIsProvider_ReturnsNullForAnyTargetRole(
        OrganizationUserType targetRole)
    {
        var result = _sut.CanManage(actingUser: null, TargetUser(targetRole), actingUserIsProvider: true);

        Assert.Null(result);
    }

    [Theory]
    // A Custom user without the ManageUsers permission has no authority over any member, even one they could
    // otherwise act on by rank.
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanManage_WhenCustomUserLacksManageUsers_ReturnsCannotManageTargetUser(
        OrganizationUserType targetRole)
    {
        var actingUser = ActingUser(OrganizationUserType.Custom, manageUsers: false);

        var result = _sut.CanManage(actingUser, TargetUser(targetRole), actingUserIsProvider: false);

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Fact]
    public void CanManage_WhenDemotingOwner_RejectsViaCurrentRole()
    {
        // An Admin demoting an Owner to User must be rejected. A member carrying the new role (User) is within
        // the Admin's authority, so escalation is only caught when the caller also checks the *current* member.
        var admin = ActingUser(OrganizationUserType.Admin);

        var currentRoleResult = _sut.CanManage(admin, TargetUser(OrganizationUserType.Owner), actingUserIsProvider: false);
        var newRoleResult = _sut.CanManage(admin, TargetUser(OrganizationUserType.User), actingUserIsProvider: false);

        Assert.IsType<CannotManageTargetUser>(currentRoleResult);
        Assert.Null(newRoleResult);
    }
}
