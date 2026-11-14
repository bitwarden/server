using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
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
    public async Task ValidateAsync_WhenEligibilityFails_ReturnsEligibilityError(
        Organization organization, OrganizationInviteLink inviteLink, User user, OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        SetupJoinRequirementsPass(sutProvider);
        // Revoked would also fail the membership status check, so this proves eligibility is checked first.
        existingOrganizationUser.Status = OrganizationUserStatusType.Revoked;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);
        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateEligibility(
                inviteLink,
                request.Code,
                organization,
                user)
            .Returns(new EmailDomainNotAllowedError(organization.Name));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowedError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithAcceptLinkAndAcceptedMembership_ReturnsAlreadyOrganizationMember(
        Organization organization, OrganizationInviteLink inviteLink, User user, OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        SetupJoinRequirementsPass(sutProvider);
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<AlreadyOrganizationMemberError>(result.AsError);
        await sutProvider.GetDependency<IInviteLinkValidationService>()
            .DidNotReceiveWithAnyArgs()
            .ValidateSeatAvailabilityAsync(Arg.Any<Organization>(), Arg.Any<OrganizationUser?>());
        await sutProvider.GetDependency<IInviteLinkValidationService>()
            .DidNotReceiveWithAnyArgs()
            .ValidateJoinRequirementsAsync(
                Arg.Any<Organization>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>());
    }

    // Confirm promotes an Accepted membership, so the same membership that blocks an accept link is allowed here.
    [Theory, BitAutoData]
    public async Task ValidateAsync_WithConfirmationLinkAndAcceptedMembership_Succeeds(
        Organization organization, OrganizationInviteLink inviteLink, User user, OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        SetupSeatsAvailable(sutProvider);
        SetupJoinRequirementsPass(sutProvider);
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);
        inviteLink.SupportsConfirmation = true;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenNoSeatsAvailable_ReturnsNoAvailableSeats(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        SetupJoinRequirementsPass(sutProvider);
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser: null);
        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateSeatAvailabilityAsync(organization, null)
            .Returns(new OrganizationHasNoAvailableSeatsError(organization.DisplayName()));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeatsError>(result.AsError);
        await sutProvider.GetDependency<IInviteLinkValidationService>()
            .DidNotReceiveWithAnyArgs()
            .ValidateJoinRequirementsAsync(
                Arg.Any<Organization>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>());
    }

    public static IEnumerable<object[]> JoinRequirementsErrors() =>
    [
        [new OnlyOneFreeOrganizationAdminAllowedError()],
        [new ProviderUsersCannotJoinError()],
        [new TwoFactorRequiredForMembershipError()],
    ];

    [Theory]
    [BitMemberAutoData(nameof(JoinRequirementsErrors))]
    public async Task ValidateAsync_WhenJoinRequirementsFail_ReturnsTheCodedError(
        InviteLinkValidationError joinRequirementsError,
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        SetupSeatsAvailable(sutProvider);
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser: null);
        var policyEnforcer = Substitute.For<IPreAccessPolicyEnforcer>();
        sutProvider.GetDependency<IPreAccessEnforcerQuery>()
            .RunAsync(organization.Id)
            .Returns(policyEnforcer);
        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateJoinRequirementsAsync(
                organization,
                user,
                OrganizationUserType.User,
                policyEnforcer)
            .Returns(new JoinRequirementsValidationResult(joinRequirementsError, AutoConfirmPolicyEnabled: false));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(joinRequirementsError, result.AsError);
    }

    private static void SetupSeatsAvailable(SutProvider<GetOrganizationInviteValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateSeatAvailabilityAsync(Arg.Any<Organization>(), Arg.Any<OrganizationUser?>())
            .Returns((InviteLinkValidationError?)null);

    private static void SetupJoinRequirementsPass(SutProvider<GetOrganizationInviteValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateJoinRequirementsAsync(
                Arg.Any<Organization>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns(new JoinRequirementsValidationResult(null, AutoConfirmPolicyEnabled: false));

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
