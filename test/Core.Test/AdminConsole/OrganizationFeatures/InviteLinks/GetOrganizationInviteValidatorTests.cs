using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
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
        inviteLink.AllowedDomains = "[\"allowed.com\"]";

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
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
        Assert.IsType<AlreadyOrganizationMember>(result.AsError);
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
    public async Task ValidateAsync_WithConfirmationLinkAndAcceptedMembership_ReturnsValidRequest(
        Organization organization, OrganizationInviteLink inviteLink, User user, OrganizationUser existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        SetupJoinRequirementsPass(sutProvider);
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
        var request = BuildRequest(organization, inviteLink, user, existingOrganizationUser);
        inviteLink.SupportsConfirmation = true;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenJoinRequirementsFail_ReturnsJoinRequirementsError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
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
            .Returns(new JoinRequirementsValidationResult(new TwoFactorRequiredForMembership(), AutoConfirmPolicyEnabled: false));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

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
