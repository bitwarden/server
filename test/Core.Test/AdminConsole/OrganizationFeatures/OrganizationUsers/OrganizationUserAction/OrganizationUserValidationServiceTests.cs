using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

public class OrganizationUserValidationServiceTests
{
    private static readonly Guid _actingUserId = Guid.NewGuid();
    private static readonly Guid _organizationId = Guid.NewGuid();

    private readonly IProviderUserRepository _providerUserRepository = Substitute.For<IProviderUserRepository>();
    private readonly OrganizationUserValidationService _sut;

    public OrganizationUserValidationServiceTests()
    {
        _sut = new OrganizationUserValidationService(_providerUserRepository);
    }

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
        new() { Type = role, OrganizationId = _organizationId };

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
    public async Task CanManage_WhenTargetRoleWithinAuthority_ReturnsNull(
        OrganizationUserType actingRole,
        OrganizationUserType targetRole)
    {
        var result = await _sut.CanManage(_actingUserId, ActingUser(actingRole), TargetUser(targetRole));

        Assert.Null(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.Admin)]
    public async Task CanManage_WhenTargetRoleOutranksActingUser_ReturnsCannotManageTargetUser(
        OrganizationUserType actingRole,
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);

        var result = await _sut.CanManage(_actingUserId, ActingUser(actingRole), TargetUser(targetRole));

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Theory]
    // A regular User has no management authority over any role.
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task CanManage_WhenActingUserIsRegularUser_ReturnsCannotManageTargetUser(
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);

        var result = await _sut.CanManage(_actingUserId, ActingUser(OrganizationUserType.User), TargetUser(targetRole));

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Theory]
    // A provider user has Owner-level authority and is not an organization member, so their membership is null and
    // authority comes solely from provider status.
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task CanManage_WhenActingUserIsProvider_ReturnsNullForAnyTargetRole(
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([new ProviderUserOrganizationDetails { OrganizationId = _organizationId }]);

        var result = await _sut.CanManage(_actingUserId, actingUser: null, TargetUser(targetRole));

        Assert.Null(result);
    }

    [Theory]
    // A Custom user without the ManageUsers permission has no authority over any member, even one they could
    // otherwise act on by rank.
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task CanManage_WhenCustomUserLacksManageUsers_ReturnsCannotManageTargetUser(
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);
        var actingUser = ActingUser(OrganizationUserType.Custom, manageUsers: false);

        var result = await _sut.CanManage(_actingUserId, actingUser, TargetUser(targetRole));

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Fact]
    public async Task CanManage_WhenDemotingOwner_RejectsViaCurrentRole()
    {
        // An Admin demoting an Owner to User must be rejected. A member carrying the new role (User) is within
        // the Admin's authority, so escalation is only caught when the caller also checks the *current* member.
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);
        var admin = ActingUser(OrganizationUserType.Admin);

        var currentRoleResult = await _sut.CanManage(_actingUserId, admin, TargetUser(OrganizationUserType.Owner));
        var newRoleResult = await _sut.CanManage(_actingUserId, admin, TargetUser(OrganizationUserType.User));

        Assert.IsType<CannotManageTargetUser>(currentRoleResult);
        Assert.Null(newRoleResult);
    }
}
