using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

public class InviteLinkRulesTests
{
    [Theory, BitAutoData]
    public void ValidateAcceptMembershipStatus_WithNoMembership_ReturnsNull(Organization organization)
    {
        // Act
        var error = InviteLinkRules.ValidateAcceptMembershipStatus(organization, null);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public void ValidateAcceptMembershipStatus_WithRevokedMembership_ReturnsOrganizationAccessRevoked(
        Organization organization, OrganizationUser existingOrganizationUser)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Revoked;

        // Act
        var error = InviteLinkRules.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.IsType<OrganizationAccessRevoked>(error);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void ValidateAcceptMembershipStatus_WithAcceptedOrConfirmedMembership_ReturnsAlreadyOrganizationMember(
        OrganizationUserStatusType status, Organization organization, OrganizationUser existingOrganizationUser)
    {
        // Arrange
        existingOrganizationUser.Status = status;

        // Act
        var error = InviteLinkRules.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.IsType<AlreadyOrganizationMember>(error);
    }

    [Theory, BitAutoData]
    public void ValidateAcceptMembershipStatus_WithPendingInvitation_ReturnsNull(
        Organization organization, OrganizationUser existingOrganizationUser)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Invited;

        // Act
        var error = InviteLinkRules.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public void ValidateConfirmMembershipStatus_WithNoMembership_ReturnsNull(Organization organization)
    {
        // Act
        var error = InviteLinkRules.ValidateConfirmMembershipStatus(organization, null);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public void ValidateConfirmMembershipStatus_WithRevocationReason_ReturnsOrganizationAccessRevoked(
        Organization organization, OrganizationUser existingOrganizationUser)
    {
        // Arrange
        existingOrganizationUser.RevocationReason = RevocationReason.Manual;

        // Act
        var error = InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.IsType<OrganizationAccessRevoked>(error);
    }

    [Theory, BitAutoData]
    public void ValidateConfirmMembershipStatus_WithConfirmedMembership_ReturnsAlreadyOrganizationMember(
        Organization organization, OrganizationUser existingOrganizationUser)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Confirmed;
        existingOrganizationUser.RevocationReason = null;

        // Act
        var error = InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.IsType<AlreadyOrganizationMember>(error);
    }

    // Confirm promotes an Accepted membership (e.g. an SSO JIT user replaying the confirm link).
    [Theory, BitAutoData]
    public void ValidateConfirmMembershipStatus_WithAcceptedMembership_ReturnsNull(
        Organization organization, OrganizationUser existingOrganizationUser)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;

        // Act
        var error = InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.Null(error);
    }
}
