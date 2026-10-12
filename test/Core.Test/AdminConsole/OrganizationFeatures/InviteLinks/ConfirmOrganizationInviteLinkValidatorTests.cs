using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
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
    // The two confirmations that consume a seat: a brand-new membership (no existing row), or a Staged row,
    // which is excluded from the occupied seat count.
    public static IEnumerable<object?[]> SeatConsumingMemberships() =>
    [
        [null],
        [new OrganizationUser { Status = OrganizationUserStatusType.Staged, Type = OrganizationUserType.User }],
    ];

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithLinkNotFound_ReturnsInviteLinkNotFound(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider) with { InviteLink = null };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithCodeMismatch_ReturnsInviteLinkNotFound(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider) with { Code = Guid.NewGuid() };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithOrganizationNotFound_ReturnsInviteLinkNotFound(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider) with { Organization = null };

        var result = await sutProvider.Sut.ValidateAsync(request);

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
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.Enabled = false;

        var result = await sutProvider.Sut.ValidateAsync(request);

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
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.UseInviteLinks = false;

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ConfirmInviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithLinkThatDoesNotSupportConfirmation_ReturnsConfirmationNotSupported(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);
        inviteLink.SupportsConfirmation = false;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        var error = Assert.IsType<ConfirmInviteLinkConfirmationNotSupported>(result.AsError);
        Assert.IsAssignableFrom<IValidationError>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithUnverifiedEmail_ReturnsEmailNotVerified(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);
        user.EmailVerified = false;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        var error = Assert.IsType<ConfirmEmailNotVerified>(result.AsError);
        Assert.IsAssignableFrom<IValidationError>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithEmailDomainNotAllowed_ReturnsEmailDomainNotAllowed(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);
        inviteLink.AllowedDomains = "[\"allowed.example.com\"]";
        user.Email = "user@notallowed.example.com";

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ConfirmEmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithRevokedMember_ReturnsOrganizationAccessRevoked(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser revokedOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        revokedOrganizationUser.RevocationReason = RevocationReason.Manual;
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = revokedOrganizationUser };

        var result = await sutProvider.Sut.ValidateAsync(request);

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
        existingOrganizationUser.Status = OrganizationUserStatusType.Confirmed;
        existingOrganizationUser.RevocationReason = null;
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = existingOrganizationUser };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ConfirmAlreadyOrganizationMember>(result.AsError);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Staged)]
    public async Task ValidateAsync_WithUnconfirmedExistingMember_IsAllowed(
        OrganizationUserStatusType status,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        existingOrganizationUser.Status = status;
        existingOrganizationUser.RevocationReason = null;
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = existingOrganizationUser };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitMemberAutoData(nameof(SeatConsumingMemberships))]
    public async Task ValidateAsync_WithSeatConsumingMembershipAndNoSeatsAvailable_ReturnsOrganizationHasNoAvailableSeats(
        OrganizationUser? existingOrganizationUser,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = existingOrganizationUser };
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 4, Sponsored = 0 });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ConfirmOrganizationHasNoAvailableSeats>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithExistingInvitedUser_SkipsSeatCheck(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser invitedOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        invitedOrganizationUser.Status = OrganizationUserStatusType.Invited;
        invitedOrganizationUser.Email = user.Email;
        invitedOrganizationUser.UserId = null;
        invitedOrganizationUser.RevocationReason = null;
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = invitedOrganizationUser };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);

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
        existingOrganizationUser.Type = OrganizationUserType.Admin;
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = existingOrganizationUser };
        organization.PlanType = PlanType.Free;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(1);

        var result = await sutProvider.Sut.ValidateAsync(request);

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
        existingOrganizationUser.Type = OrganizationUserType.Admin;
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = existingOrganizationUser };
        organization.PlanType = PlanType.Free;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(0);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithPolicyError_ReturnsMappedValidationError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateCanJoinAsync(Arg.Any<Organization>(), Arg.Any<User>(), Arg.Any<OrganizationUserType>())
            .Returns(new TwoFactorRequiredForMembership());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        var error = Assert.IsType<ConfirmTwoFactorRequiredForMembership>(result.AsError);
        Assert.IsAssignableFrom<IValidationError>(error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithEarlierCheckFailing_DoesNotValidatePolicies(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);
        user.EmailVerified = false;

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        await sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .DidNotReceiveWithAnyArgs()
            .ValidateCanJoinAsync(default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenAllChecksPass_ReturnsValidRequestAndPassesContextToPolicyCheck(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        existingOrganizationUser.Status = OrganizationUserStatusType.Invited;
        existingOrganizationUser.RevocationReason = null;
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider)
            with
        { ExistingOrganizationUser = existingOrganizationUser };

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Same(request, result.Request);

        await sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>().Received(1)
            .ValidateCanJoinAsync(organization, user, existingOrganizationUser.Type);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNewMember_ChecksPoliciesAsUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        var request = SetupHappyPath(organization, inviteLink, user, sutProvider);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
        await sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>().Received(1)
            .ValidateCanJoinAsync(organization, user, OrganizationUserType.User);
    }

    private static ConfirmOrganizationInviteLinkValidationRequest SetupHappyPath(
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
        link.Code = Guid.NewGuid().ToString();
        link.AllowedDomains = "[\"example.com\"]";
        link.SupportsConfirmation = true;
        user.Email = "user@example.com";
        user.EmailVerified = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(org.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        sutProvider.GetDependency<IPricingClient>()
            .GetPlan(Arg.Any<PlanType>())
            .Returns(new Enterprise2023Plan(isAnnual: true));

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateCanJoinAsync(Arg.Any<Organization>(), Arg.Any<User>(), Arg.Any<OrganizationUserType>())
            .Returns((Error?)null);

        return new ConfirmOrganizationInviteLinkValidationRequest
        {
            InviteLink = link,
            Code = Guid.Parse(link.Code),
            Organization = org,
            User = user,
        };
    }
}
