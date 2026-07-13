using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class ConfirmOrganizationInviteLinkValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WithLinkNotFound_ReturnsInviteLinkNotFound(
        ConfirmOrganizationInviteLinkValidationRequest request,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithOrganizationNotFound_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(inviteLink.Code)
            .Returns(inviteLink);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithOrganizationDisabled_ReturnsInviteLinkNotFound(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        organization.Enabled = false;
        inviteLink.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(inviteLink.Code)
            .Returns(inviteLink);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithOrganizationNotUsingInviteLinks_ReturnsInviteLinkNotAvailable(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        organization.Enabled = true;
        organization.UseInviteLinks = false;
        inviteLink.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(inviteLink.Code)
            .Returns(inviteLink);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmInviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithEmailDomainNotAllowed_ReturnsEmailDomainNotAllowed(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        inviteLink.AllowedDomains = "[\"allowed.example.com\"]";
        user.Email = "user@notallowed.example.com";

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmEmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithProviderUser_ReturnsProviderUsersCannotAcceptInviteLink(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        Bit.Core.AdminConsole.Entities.Provider.ProviderUser providerUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([providerUser]);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmProviderUsersCannotAcceptInviteLink>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithRevokedMember_ReturnsOrganizationAccessRevoked(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser revokedOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        revokedOrganizationUser.RevocationReason = RevocationReason.Manual;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(revokedOrganizationUser);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOrganizationAccessRevoked>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithRevokedEmailInvite_ReturnsOrganizationAccessRevoked(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser revokedEmailInvite,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        revokedEmailInvite.RevocationReason = RevocationReason.Manual;
        revokedEmailInvite.Email = user.Email;
        revokedEmailInvite.UserId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(revokedEmailInvite);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOrganizationAccessRevoked>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithConfirmedMember_ReturnsAlreadyOrganizationMember(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        existingOrganizationUser.Status = OrganizationUserStatusType.Confirmed;
        existingOrganizationUser.RevocationReason = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmAlreadyOrganizationMember>(result.AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    public async Task ValidateAsync_WithUnconfirmedExistingMember_IsAllowed(
        OrganizationUserStatusType status,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        existingOrganizationUser.Status = status;
        existingOrganizationUser.RevocationReason = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(existingOrganizationUser, result.AsSuccess.ExistingOrganizationUser);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNewUserAndNoSeatsAvailable_ReturnsOrganizationHasNoAvailableSeats(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;

        sutProvider.GetDependency<IPricingClient>()
            .GetPlan(organization.PlanType)
            .Returns(new Enterprise2023Plan(isAnnual: true));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 4, Sponsored = 0 });

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOrganizationHasNoAvailableSeats>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSingleOrganizationPolicyViolation_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser membershipInAnotherOrganization,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        // Single Org enabled for the target org, and the user belongs to a different org.
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement(
                [new PolicyDetails { OrganizationId = organization.Id }]));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([membershipInAnotherOrganization]);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        var error = Assert.IsType<ConfirmUserIsAMemberOfAnotherOrganization>(result.AsError);
        // Confirm errors must map to a validation problem so the endpoint returns the RFC 7807 shape.
        Assert.IsAssignableFrom<IValidationError>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenMemberOfAnotherOrganizationWithSingleOrgPolicy_ReturnsMappedValidationError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        Guid otherOrganizationId,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        // The target org does not enforce Single Org, but another org the user belongs to does.
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = otherOrganizationId,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                }
            ]));

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        // The shared policy error is translated to the link-confirm variant, which is a validation problem.
        var error = Assert.IsType<ConfirmUserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result.AsError);
        Assert.IsAssignableFrom<IValidationError>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithTwoFactorRequiredAndUserLacks2FA_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
                [new PolicyDetails { OrganizationId = organization.Id }]));

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmTwoFactorRequiredForMembership>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithTwoFactorRequiredAndUserHas2FA_IsAllowed(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
                [new PolicyDetails { OrganizationId = organization.Id }]));

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(true);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNewUser_ReturnsValidatedContext(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(inviteLink, result.AsSuccess.InviteLink);
        Assert.Same(organization, result.AsSuccess.Organization);
        Assert.Null(result.AsSuccess.ExistingOrganizationUser);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithExistingInvitedUser_SkipsSeatCheckAndReturnsContext(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser invitedOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        invitedOrganizationUser.Status = OrganizationUserStatusType.Invited;
        invitedOrganizationUser.Email = user.Email;
        invitedOrganizationUser.UserId = null;
        invitedOrganizationUser.RevocationReason = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(invitedOrganizationUser);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(invitedOrganizationUser, result.AsSuccess.ExistingOrganizationUser);

        // An existing invite already occupies a seat, so no Password Manager seat check is performed.
        await sutProvider.GetDependency<IPricingClient>()
            .DidNotReceiveWithAnyArgs()
            .GetPlan(Arg.Any<PlanType>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithFreeAdminMembership_AdminLimitReached_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.PlanType = PlanType.Free;
        existingOrganizationUser.Type = OrganizationUserType.Admin;
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(1);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOnlyOneFreeOrganizationAdminAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithFreeAdminMembership_NotAtLimit_IsAllowed(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.PlanType = PlanType.Free;
        existingOrganizationUser.Type = OrganizationUserType.Admin;
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(0);

        // Act
        var request = new ConfirmOrganizationInviteLinkValidationRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(existingOrganizationUser, result.AsSuccess.ExistingOrganizationUser);
    }

    private static void SetupHappyPath(
        Organization org,
        OrganizationInviteLink link,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        org.Enabled = true;
        org.UseInviteLinks = true;
        org.Seats = 10;
        org.MaxAutoscaleSeats = null;
        org.PlanType = PlanType.EnterpriseAnnually;
        link.OrganizationId = org.Id;
        link.AllowedDomains = "[\"example.com\"]";
        user.Email = "user@example.com";

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(link.Code)
            .Returns(link);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(org.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        sutProvider.GetDependency<IPricingClient>()
            .GetPlan(org.PlanType)
            .Returns(new Enterprise2023Plan(isAnnual: true));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(org.Id, user.Email)
            .Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([]);

        // No policies enforced by default.
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement([]));
    }
}
