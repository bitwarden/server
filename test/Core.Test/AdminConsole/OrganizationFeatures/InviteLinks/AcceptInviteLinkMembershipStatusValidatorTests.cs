using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class AcceptInviteLinkMembershipStatusValidatorTests
{
    [Theory, BitAutoData]
    public void Validate_WithRevokedMembership_ReturnsOrganizationAccessRevoked(
        Organization organization, OrganizationUser existingOrganizationUser,
        SutProvider<AcceptInviteLinkMembershipStatusValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Revoked;

        // Act
        var result = sutProvider.Sut.Validate(BuildRequest(organization, existingOrganizationUser));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationAccessRevoked>(result.AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void Validate_WithAcceptedOrConfirmedMembership_ReturnsAlreadyOrganizationMember(
        OrganizationUserStatusType status,
        Organization organization, OrganizationUser existingOrganizationUser,
        SutProvider<AcceptInviteLinkMembershipStatusValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = status;

        // Act
        var result = sutProvider.Sut.Validate(BuildRequest(organization, existingOrganizationUser));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<AlreadyOrganizationMember>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithPendingInvitation_ReturnsValidRequest(
        Organization organization, OrganizationUser existingOrganizationUser,
        SutProvider<AcceptInviteLinkMembershipStatusValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Invited;
        var request = BuildRequest(organization, existingOrganizationUser);

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(request, result.Request);
    }

    private static InviteLinkMembershipStatusValidationRequest BuildRequest(
        Organization organization, OrganizationUser? existingOrganizationUser) =>
        new() { Organization = organization, ExistingOrganizationUser = existingOrganizationUser };
}
