using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkEligibilityValidationServiceTests
{
    [Theory, BitAutoData]
    public void ValidateEligibility_WithLinkNotFound_ReturnsInviteLinkNotFound(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            null,
            code,
            organization,
            user);

        // Assert
        Assert.IsType<InviteLinkNotFound>(error);
    }

    [Theory, BitAutoData]
    public void ValidateEligibility_WithCodeMismatch_ReturnsInviteLinkNotFound(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        SetupEligible(organization, inviteLink, user);

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            Guid.NewGuid(),
            organization,
            user);

        // Assert
        Assert.IsType<InviteLinkNotFound>(error);
    }

    [Theory, BitAutoData]
    public void ValidateEligibility_WithOrganizationNotFound_ReturnsInviteLinkNotFound(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            null,
            user);

        // Assert
        Assert.IsType<InviteLinkNotFound>(error);
    }

    [Theory, BitAutoData]
    public void ValidateEligibility_WithOrganizationDisabled_ReturnsInviteLinkNotFound(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);
        organization.Enabled = false;

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            organization,
            user);

        // Assert
        Assert.IsType<InviteLinkNotFound>(error);
    }

    [Theory, BitAutoData]
    public void ValidateEligibility_WithOrganizationNotUsingInviteLinks_ReturnsInviteLinkNotAvailable(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);
        organization.UseInviteLinks = false;

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            organization,
            user);

        // Assert
        Assert.IsType<InviteLinkNotAvailable>(error);
    }

    [Theory, BitAutoData]
    public void ValidateEligibility_WithDisallowedDomain_ReturnsEmailDomainNotAllowed(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);
        inviteLink.AllowedDomains = "[\"allowed.com\"]";

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            organization,
            user);

        // Assert
        Assert.IsType<EmailDomainNotAllowed>(error);
    }

    // Auth's invite link email verification flow (PM-42435) relies on the domain being checked first.
    [Theory, BitAutoData]
    public void ValidateEligibility_WithUnverifiedEmailAndDisallowedDomain_ReturnsEmailDomainNotAllowedFirst(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);
        inviteLink.AllowedDomains = "[\"allowed.com\"]";
        user.EmailVerified = false;

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            organization,
            user);

        // Assert
        Assert.IsType<EmailDomainNotAllowed>(error);
    }

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [BitAutoData(true, true)]
    [BitAutoData(true, false)]
    public void ValidateEligibility_WithVerifiedEmail_ReturnsNull(
        bool selfHosted,
        bool enableOpenInviteLinkEmailVerification,
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = selfHosted;
        sutProvider.GetDependency<IGlobalSettings>().EnableOpenInviteLinkEmailVerification = enableOpenInviteLinkEmailVerification;

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            organization,
            user);

        // Assert
        Assert.Null(error);
    }

    // Cloud always requires email verification, even if the setting is disabled.
    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [BitAutoData(true, true)]
    public void ValidateEligibility_WithUnverifiedEmailAndVerificationRequired_ReturnsEmailNotVerified(
        bool selfHosted,
        bool enableOpenInviteLinkEmailVerification,
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);
        user.EmailVerified = false;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = selfHosted;
        sutProvider.GetDependency<IGlobalSettings>().EnableOpenInviteLinkEmailVerification = enableOpenInviteLinkEmailVerification;

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            organization,
            user);

        // Assert
        Assert.IsType<EmailNotVerified>(error);
    }

    [Theory, BitAutoData]
    public void ValidateEligibility_WithUnverifiedEmailSelfHostedAndSettingDisabled_ReturnsNull(
        SutProvider<InviteLinkEligibilityValidationService> sutProvider,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user)
    {
        // Arrange
        var code = SetupEligible(organization, inviteLink, user);
        user.EmailVerified = false;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IGlobalSettings>().EnableOpenInviteLinkEmailVerification = false;

        // Act
        var error = sutProvider.Sut.ValidateEligibility(
            inviteLink,
            code,
            organization,
            user);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public void ValidateAcceptMembershipStatus_WithNoMembership_ReturnsNull(Organization organization)
    {
        // Act
        var error = InviteLinkEligibilityValidationService.ValidateAcceptMembershipStatus(organization, null);

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
        var error = InviteLinkEligibilityValidationService.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

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
        var error = InviteLinkEligibilityValidationService.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

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
        var error = InviteLinkEligibilityValidationService.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public void ValidateConfirmMembershipStatus_WithNoMembership_ReturnsNull(Organization organization)
    {
        // Act
        var error = InviteLinkEligibilityValidationService.ValidateConfirmMembershipStatus(organization, null);

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
        var error = InviteLinkEligibilityValidationService.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);

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
        var error = InviteLinkEligibilityValidationService.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);

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
        var error = InviteLinkEligibilityValidationService.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);

        // Assert
        Assert.Null(error);
    }

    // Makes the link, organization, and user pass every eligibility check and returns the link's code.
    private static Guid SetupEligible(Organization organization, OrganizationInviteLink inviteLink, User user)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.AllowedDomains = "[\"example.com\"]";
        user.Email = "user@example.com";
        user.EmailVerified = true;

        return Guid.Parse(inviteLink.Code);
    }
}
