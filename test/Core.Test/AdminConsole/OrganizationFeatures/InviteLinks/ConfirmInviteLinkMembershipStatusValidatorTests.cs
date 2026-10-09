using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class ConfirmInviteLinkMembershipStatusValidatorTests
{
    [Theory, BitAutoData]
    public void Validate_WithRevocationReason_ReturnsOrganizationAccessRevoked(
        Organization organization, OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmInviteLinkMembershipStatusValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.RevocationReason = RevocationReason.Manual;

        // Act
        var result = sutProvider.Sut.Validate(BuildRequest(organization, existingOrganizationUser));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationAccessRevoked>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithConfirmedMembership_ReturnsAlreadyOrganizationMember(
        Organization organization, OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmInviteLinkMembershipStatusValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Confirmed;
        existingOrganizationUser.RevocationReason = null;

        // Act
        var result = sutProvider.Sut.Validate(BuildRequest(organization, existingOrganizationUser));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<AlreadyOrganizationMember>(result.AsError);
    }

    // Confirm promotes an Accepted membership (e.g. an SSO JIT user replaying the confirm link).
    [Theory, BitAutoData]
    public void Validate_WithAcceptedMembership_ReturnsValidRequest(
        Organization organization, OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmInviteLinkMembershipStatusValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
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
