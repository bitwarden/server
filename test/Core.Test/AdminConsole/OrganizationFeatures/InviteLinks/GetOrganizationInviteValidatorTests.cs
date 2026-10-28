using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class GetOrganizationInviteValidatorTests
{
    [Theory, BitAutoData]
    public void Validate_WhenAllChecksPass_ReturnsValidRequest(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser: null);

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(request, result.Request);
    }

    [Theory, BitAutoData]
    public void Validate_WhenEligibilityFails_ReturnsEligibilityError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        // Revoked would also fail the membership status check, so this proves eligibility is checked first.
        existingOrganizationUser.Status = OrganizationUserStatusType.Revoked;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);
        sutProvider.GetDependency<IInviteLinkRules>()
            .ValidateEligibility(
                inviteLink,
                request.Code,
                organization,
                user)
            .Returns(new EmailDomainNotAllowed(organization.Name));

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithAcceptLinkAndAcceptedMembership_ReturnsAlreadyOrganizationMember(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<AlreadyOrganizationMember>(result.AsError);
    }

    // Confirm promotes an Accepted membership, so the same membership that blocks an accept link is allowed here.
    [Theory, BitAutoData]
    public void Validate_WithConfirmationLinkAndAcceptedMembership_ReturnsValidRequest(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);
        inviteLink.SupportsConfirmation = true;

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public void Validate_WithConfirmationLinkAndRevokedMembership_ReturnsOrganizationAccessRevoked(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.RevocationReason = RevocationReason.Manual;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);
        inviteLink.SupportsConfirmation = true;

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationAccessRevoked>(result.AsError);
    }

    // Builds a request that passes every eligibility check, for an accept link.
    private static OrganizationInviteLinkValidationRequest BuildRequest(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser? existingOrganizationUser)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.AllowedDomains = "[\"example.com\"]";
        inviteLink.SupportsConfirmation = false;
        user.Email = "user@example.com";
        user.EmailVerified = true;

        return new OrganizationInviteLinkValidationRequest
        {
            ExistingInviteLink = inviteLink,
            Code = Guid.Parse(inviteLink.Code),
            Organization = organization,
            User = user,
            ExistingOrganizationUser = existingOrganizationUser,
        };
    }
}
