using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

public class ManageOrganizationUserValidationServiceTests
{
    private static readonly Guid _actingUserId = Guid.NewGuid();
    private static readonly Guid _organizationId = Guid.NewGuid();

    private readonly IProviderUserRepository _providerUserRepository = Substitute.For<IProviderUserRepository>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly ManageOrganizationUserValidationService _sut;

    public ManageOrganizationUserValidationServiceTests()
    {
        _sut = new ManageOrganizationUserValidationService(_providerUserRepository, _organizationUserRepository);
    }

    // NOTE: A null `actingUser` represents a non-member (provider-only user). Custom users are granted the
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
    public async Task CanManageAsync_WhenTargetRoleWithinAuthority_ReturnsNull(
        OrganizationUserType actingRole,
        OrganizationUserType targetRole)
    {
        var result = await _sut.CanManageAsync(_actingUserId, ActingUser(actingRole), TargetUser(targetRole));

        Assert.Null(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom, OrganizationUserType.Admin)]
    public async Task CanManageAsync_WhenTargetRoleOutranksActingUser_ReturnsCannotManageTargetUser(
        OrganizationUserType actingRole,
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);

        var result = await _sut.CanManageAsync(_actingUserId, ActingUser(actingRole), TargetUser(targetRole));

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Theory]
    // A regular User has no management authority over any role.
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task CanManageAsync_WhenActingUserIsRegularUser_ReturnsCannotManageTargetUser(
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);

        var result = await _sut.CanManageAsync(_actingUserId, ActingUser(OrganizationUserType.User), TargetUser(targetRole));

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Theory]
    // A provider user has Owner-level authority and is not an organization member, so their membership is null and
    // authority comes solely from provider status.
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task CanManageAsync_WhenActingUserIsProvider_ReturnsNullForAnyTargetRole(
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([new ProviderUserOrganizationDetails { OrganizationId = _organizationId }]);

        var result = await _sut.CanManageAsync(_actingUserId, actingUser: null, TargetUser(targetRole));

        Assert.Null(result);
    }

    [Theory]
    // A Custom user without the ManageUsers permission has no authority over any member, even one they could
    // otherwise act on by rank.
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task CanManageAsync_WhenCustomUserLacksManageUsers_ReturnsCannotManageTargetUser(
        OrganizationUserType targetRole)
    {
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);
        var actingUser = ActingUser(OrganizationUserType.Custom, manageUsers: false);

        var result = await _sut.CanManageAsync(_actingUserId, actingUser, TargetUser(targetRole));

        Assert.IsType<CannotManageTargetUser>(result);
    }

    [Fact]
    public async Task CanManageAsync_WhenDemotingOwner_RejectsViaCurrentRole()
    {
        // An Admin demoting an Owner to User must be rejected. A member carrying the new role (User) is within
        // the Admin's authority, so escalation is only caught when the caller also checks the *current* member.
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);
        var admin = ActingUser(OrganizationUserType.Admin);

        var currentRoleResult = await _sut.CanManageAsync(_actingUserId, admin, TargetUser(OrganizationUserType.Owner));
        var newRoleResult = await _sut.CanManageAsync(_actingUserId, admin, TargetUser(OrganizationUserType.User));

        Assert.IsType<CannotManageTargetUser>(currentRoleResult);
        Assert.Null(newRoleResult);
    }

    [Fact]
    public async Task CanManageRoleChangeAsync_WhenActingUserCanManageBothRoles_ReturnsNull()
    {
        // An Admin promoting a User to Custom can manage both the current and new role.
        var result = await _sut.CanManageRoleChangeAsync(_actingUserId, ActingUser(OrganizationUserType.Admin)!,
            TargetUser(OrganizationUserType.User), OrganizationUserType.Custom, targetNewPermissions: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task CanManageRoleChangeAsync_WhenDeniedAndNoOwnerInvolved_ReturnsCustomUsersCannotManageAdminsOrOwners()
    {
        // A Custom user can't promote a User to Admin. Neither role is Owner, so the denial maps to the custom-user error.
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);

        var result = await _sut.CanManageRoleChangeAsync(_actingUserId, ActingUser(OrganizationUserType.Custom)!,
            TargetUser(OrganizationUserType.User), OrganizationUserType.Admin, targetNewPermissions: null);

        Assert.IsType<CustomUsersCannotManageAdminsOrOwners>(result);
    }

    [Fact]
    public async Task CanManageRoleChangeAsync_WhenTargetIsOwner_ReturnsOnlyOwnersCanManageOwners()
    {
        // An Admin can't manage an Owner, so demoting one is rejected with the owner-specific error.
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);

        var result = await _sut.CanManageRoleChangeAsync(_actingUserId, ActingUser(OrganizationUserType.Admin)!,
            TargetUser(OrganizationUserType.Owner), OrganizationUserType.User, targetNewPermissions: null);

        Assert.IsType<OnlyOwnersCanManageOwners>(result);
    }

    [Fact]
    public async Task CanManageRoleChangeAsync_WhenPromotingToOwner_ReturnsOnlyOwnersCanManageOwners()
    {
        // An Admin can manage a User but can't promote them to Owner, so the new role maps to the owner-specific error.
        _providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(_actingUserId, ProviderUserStatusType.Confirmed)
            .Returns([]);

        var result = await _sut.CanManageRoleChangeAsync(_actingUserId, ActingUser(OrganizationUserType.Admin)!,
            TargetUser(OrganizationUserType.User), OrganizationUserType.Owner, targetNewPermissions: null);

        Assert.IsType<OnlyOwnersCanManageOwners>(result);
    }

    [Fact]
    public async Task CanManageRoleChangeAsync_WhenCustomActorGrantsPermissionTheyDoNotHold_ReturnsCustomUsersCanOnlyGrantOwnPermissions()
    {
        // A Custom actor holding only ManageUsers can't grant ManageSso.
        var actingUser = CustomUser(new Permissions { ManageUsers = true });

        var result = await _sut.CanManageRoleChangeAsync(_actingUserId, actingUser, TargetUser(OrganizationUserType.Custom),
            OrganizationUserType.Custom, new Permissions { ManageSso = true });

        Assert.IsType<CustomUsersCanOnlyGrantOwnPermissions>(result);
    }

    [Fact]
    public async Task CanManageRoleChangeAsync_WhenCustomActorGrantsPermissionsTheyHold_ReturnsNull()
    {
        var actingUser = CustomUser(new Permissions { ManageUsers = true });

        var result = await _sut.CanManageRoleChangeAsync(_actingUserId, actingUser, TargetUser(OrganizationUserType.Custom),
            OrganizationUserType.Custom, new Permissions { ManageUsers = true });

        Assert.Null(result);
    }

    [Fact]
    public async Task CanManageRoleChangeAsync_WhenOwnerGrantsAnyPermission_ReturnsNull()
    {
        // Owners are exempt from the grant-subset check.
        var result = await _sut.CanManageRoleChangeAsync(_actingUserId, ActingUser(OrganizationUserType.Owner)!,
            TargetUser(OrganizationUserType.Custom), OrganizationUserType.Custom,
            new Permissions { ManageScim = true, ManageSso = true });

        Assert.Null(result);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually, OrganizationUserType.User, OrganizationUserType.Admin)]
    [InlineData(PlanType.Free, OrganizationUserType.User, OrganizationUserType.User)]
    public async Task ValidateFreeOrgAdminLimitAsync_WhenLimitDoesNotApply_ReturnsNull(
        PlanType planType, OrganizationUserType currentType, OrganizationUserType newType)
    {
        var result = await _sut.ValidateFreeOrgAdminLimitAsync(Guid.NewGuid(), planType, currentType, newType);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateFreeOrgAdminLimitAsync_WhenUserIdIsNull_ReturnsNull()
    {
        var result = await _sut.ValidateFreeOrgAdminLimitAsync(null, PlanType.Free, OrganizationUserType.User,
            OrganizationUserType.Admin);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateFreeOrgAdminLimitAsync_WhenPromotingAndAdminElsewhere_ReturnsCannotBeAdminOfMultipleFreeOrganizations()
    {
        var userId = Guid.NewGuid();
        _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(userId).Returns(1);

        var result = await _sut.ValidateFreeOrgAdminLimitAsync(userId, PlanType.Free, OrganizationUserType.User,
            OrganizationUserType.Admin);

        Assert.IsType<CannotBeAdminOfMultipleFreeOrganizations>(result);
    }

    [Fact]
    public async Task ValidateFreeOrgAdminLimitAsync_WhenPromotingAndNotAdminElsewhere_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(userId).Returns(0);

        var result = await _sut.ValidateFreeOrgAdminLimitAsync(userId, PlanType.Free, OrganizationUserType.User,
            OrganizationUserType.Admin);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateFreeOrgAdminLimitAsync_WhenAlreadyAdminOfThisOrgOnly_ReturnsNull()
    {
        // The count includes this organization, so a single free-org admin membership is allowed.
        var userId = Guid.NewGuid();
        _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(userId).Returns(1);

        var result = await _sut.ValidateFreeOrgAdminLimitAsync(userId, PlanType.Free, OrganizationUserType.Admin,
            OrganizationUserType.Owner);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateFreeOrgAdminLimitAsync_WhenAlreadyAdminAndAdminElsewhere_ReturnsCannotBeAdminOfMultipleFreeOrganizations()
    {
        var userId = Guid.NewGuid();
        _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(userId).Returns(2);

        var result = await _sut.ValidateFreeOrgAdminLimitAsync(userId, PlanType.Free, OrganizationUserType.Admin,
            OrganizationUserType.Owner);

        Assert.IsType<CannotBeAdminOfMultipleFreeOrganizations>(result);
    }

    private static OrganizationUser CustomUser(Permissions permissions)
    {
        var user = new OrganizationUser { Type = OrganizationUserType.Custom, OrganizationId = _organizationId };
        user.SetPermissions(permissions);
        return user;
    }
}
