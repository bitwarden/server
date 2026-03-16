using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

[SutProviderCustomize]
public class AutomaticUserConfirmationOrganizationPolicyComplianceValidatorTests
{
    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_AllUsersCompliant_NoProviders_ReturnsValid(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_UserInAnotherOrg_ReturnsUserNotCompliantWithSingleOrganization(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(), // Different org
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserNotCompliantWithSingleOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_ProviderUsersExist_ReturnsProviderExistsInOrganization(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        var providerUser = new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            UserId = userId
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([providerUser]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ProviderExistsInOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_InvitedUsersExcluded_FromSingleOrgCheck(
        Guid organizationId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange - invited user has null UserId and Invited status
        var invitedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Email = "invited@example.com"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([invitedUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);

        // Invited users with null UserId should not trigger the single org query
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()));
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_InvitedUserWithUserId_ExcludedFromSingleOrgCheck(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange - Invited status users are excluded regardless of UserId
        var invitedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Invited
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([invitedUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);

        // Invited users should not be included in the single org compliance query
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()));
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_UserInAnotherOrgWithInvitedStatus_ReturnsValid(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        // User has an Invited status in another org - should not count as non-compliant
        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = userId,
            Status = OrganizationUserStatusType.Invited
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_SingleOrgViolationTakesPrecedence_OverProviderCheck(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange - user is in another org AND is a provider user
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserNotCompliantWithSingleOrganization>(result.AsError);

        // Provider check should not be called since single org check failed first
        await sutProvider.GetDependency<IProviderUserRepository>()
            .DidNotReceive()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_MixedUsers_OnlyNonInvitedChecked(
        Guid organizationId,
        Guid confirmedUserId,
        Guid acceptedUserId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        var invitedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Email = "invited@example.com"
        };

        var confirmedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = confirmedUserId,
            Status = OrganizationUserStatusType.Confirmed
        };

        var acceptedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = acceptedUserId,
            Status = OrganizationUserStatusType.Accepted
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([invitedUser, confirmedUser, acceptedUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);

        // Only confirmed and accepted users should be checked for single org compliance
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 2 &&
                ids.Contains(confirmedUserId) &&
                ids.Contains(acceptedUserId)));
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_NoOrganizationUsers_ReturnsValid(
        Guid organizationId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_UserInSameOrgOnly_ReturnsValid(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        // User exists in the same org only (the GetManyByManyUsersAsync returns same-org entry)
        var sameOrgUser = new OrganizationUser
        {
            Id = orgUser.Id,
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([sameOrgUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_ProviderCheckIncludesAllUsersWithUserIds(
        Guid organizationId,
        Guid userId1,
        Guid userId2,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange - provider check includes users regardless of Invited status (only excludes null UserId)
        var confirmedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId1,
            Status = OrganizationUserStatusType.Confirmed
        };

        var invitedUserWithNullId = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Email = "invited@example.com"
        };

        var acceptedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId2,
            Status = OrganizationUserStatusType.Accepted
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([confirmedUser, invitedUserWithNullId, acceptedUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);

        // Provider check should include all users with non-null UserIds (confirmed + accepted)
        await sutProvider.GetDependency<IProviderUserRepository>()
            .Received(1)
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 2 &&
                ids.Contains(userId1) &&
                ids.Contains(userId2)));
    }

    [Theory, BitAutoData]
    public async Task IsOrganizationCompliantAsync_RevokedUserInAnotherOrg_ReturnsUserNotCompliant(
        Guid organizationId,
        Guid userId,
        SutProvider<AutomaticUserConfirmationOrganizationPolicyComplianceValidator> sutProvider)
    {
        // Arrange
        var revokedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Revoked
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationId)
            .Returns([revokedUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organizationId);

        // Act
        var result = await sutProvider.Sut.IsOrganizationCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<UserNotCompliantWithSingleOrganization>(result.AsError);
    }
}
