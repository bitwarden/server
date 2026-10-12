using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkEligibilityValidatorTests
{
    [Theory, BitAutoData]
    public void Validate_WhenAllChecksPass_ReturnsValidRequest(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user);

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(request, result.Request);
    }

    [Theory, BitAutoData]
    public void Validate_WithLinkNotFound_ReturnsInviteLinkNotFound(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user) with { InviteLink = null };

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithCodeMismatch_ReturnsInviteLinkNotFound(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user) with { Code = Guid.NewGuid() };

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithOrganizationNotFound_ReturnsInviteLinkNotFound(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user) with { Organization = null };

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithOrganizationDisabled_ReturnsInviteLinkNotFound(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user);
        organization.Enabled = false;

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithOrganizationNotUsingInviteLinks_ReturnsInviteLinkNotAvailable(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user);
        organization.UseInviteLinks = false;

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public void Validate_WithUnverifiedEmail_ReturnsEmailNotVerified(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user);
        user.EmailVerified = false;

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailNotVerified>(result.AsError);
    }

    // Auth's invite link email verification flow (PM-42435) relies on the domain being checked first.
    [Theory, BitAutoData]
    public void Validate_WithUnverifiedEmailAndDisallowedDomain_ReturnsEmailDomainNotAllowedFirst(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<InviteLinkEligibilityValidator> sutProvider)
    {
        // Arrange
        var request = BuildValidRequest(organization, inviteLink, user);
        inviteLink.AllowedDomains = "[\"allowed.com\"]";
        user.EmailVerified = false;

        // Act
        var result = sutProvider.Sut.Validate(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    private static InviteLinkEligibilityValidationRequest BuildValidRequest(
        Organization organization, OrganizationInviteLink inviteLink, User user)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.AllowedDomains = "[\"example.com\"]";
        user.Email = "user@example.com";
        user.EmailVerified = true;

        return new InviteLinkEligibilityValidationRequest
        {
            InviteLink = inviteLink,
            Code = Guid.Parse(inviteLink.Code),
            Organization = organization,
            User = user,
        };
    }
}
