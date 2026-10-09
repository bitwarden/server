using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class ConfirmOrganizationInviteLinkValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenAllChecksPass_ReturnsValidRequest(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(request, result.Request);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_PassesRequestDataToSharedValidators(
        Organization organization, OrganizationInviteLink inviteLink, User user, OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);

        // Act
        await sutProvider.Sut.ValidateAsync(request);

        // Assert
        sutProvider.GetDependency<IInviteLinkEligibilityValidator>().Received(1)
            .Validate(Arg.Is<InviteLinkEligibilityValidationRequest>(r =>
                r.InviteLink == inviteLink && r.Code == request.Code && r.Organization == organization && r.User == user));
        sutProvider.GetDependency<IConfirmInviteLinkMembershipStatusValidator>().Received(1)
            .Validate(Arg.Is<InviteLinkMembershipStatusValidationRequest>(r =>
                r.Organization == organization && r.ExistingOrganizationUser == existingOrganizationUser));
        await sutProvider.GetDependency<IInviteLinkFreeOrganizationAdminValidator>().Received(1)
            .ValidateAsync(Arg.Is<InviteLinkFreeOrganizationAdminValidationRequest>(r =>
                r.Organization == organization && r.User == user && r.ExistingOrganizationUser == existingOrganizationUser));
        await sutProvider.GetDependency<IInviteLinkOrganizationCapabilityValidator>().Received(1)
            .ValidateAsync(Arg.Is<InviteLinkOrganizationCapabilityValidationRequest>(r =>
                r.Organization == organization && r.ExistingOrganizationUser == existingOrganizationUser));
        await sutProvider.GetDependency<IInviteLinkPolicyValidator>().Received(1)
            .ValidateAsync(Arg.Is<InviteLinkPolicyValidationRequest>(r =>
                r.Organization == organization && r.User == user && r.ExistingOrganizationUser == existingOrganizationUser));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEligibilityFails_ReturnsMappedErrorBeforeOtherChecks(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        inviteLink.SupportsConfirmation = false;
        sutProvider.GetDependency<IInviteLinkEligibilityValidator>()
            .Validate(Arg.Any<InviteLinkEligibilityValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkEligibilityValidationRequest>(), new EmailNotVerified()));
        SetMembershipStatusError(sutProvider);
        SetFreeOrganizationAdminError(sutProvider);
        SetCapabilityError(sutProvider);
        SetPolicyError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmEmailNotVerified>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithLinkThatDoesNotSupportConfirmation_ReturnsConfirmationNotSupported(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        inviteLink.SupportsConfirmation = false;
        SetMembershipStatusError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmInviteLinkConfirmationNotSupported>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenMembershipStatusFails_ReturnsMappedErrorBeforeFreeOrganizationAdmin(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetMembershipStatusError(sutProvider);
        SetFreeOrganizationAdminError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmAlreadyOrganizationMember>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenFreeOrganizationAdminFails_ReturnsMappedErrorBeforeCapability(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetFreeOrganizationAdminError(sutProvider);
        SetCapabilityError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOnlyOneFreeOrganizationAdminAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenCapabilityFails_ReturnsMappedErrorBeforePolicies(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetCapabilityError(sutProvider);
        SetPolicyError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOrganizationHasNoAvailableSeats>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenPolicyFails_ReturnsMappedError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetPolicyError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmTwoFactorRequiredForMembership>(result.AsError);
    }

    private static void SetMembershipStatusError(SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider) =>
        sutProvider.GetDependency<IConfirmInviteLinkMembershipStatusValidator>()
            .Validate(Arg.Any<InviteLinkMembershipStatusValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkMembershipStatusValidationRequest>(), new AlreadyOrganizationMember("Org")));

    private static void SetFreeOrganizationAdminError(SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkFreeOrganizationAdminValidator>()
            .ValidateAsync(Arg.Any<InviteLinkFreeOrganizationAdminValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkFreeOrganizationAdminValidationRequest>(),
                new OnlyOneFreeOrganizationAdminAllowed()));

    private static void SetCapabilityError(SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkOrganizationCapabilityValidator>()
            .ValidateAsync(Arg.Any<InviteLinkOrganizationCapabilityValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkOrganizationCapabilityValidationRequest>(),
                new OrganizationHasNoAvailableSeats("Org")));

    private static void SetPolicyError(SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkPolicyValidator>()
            .ValidateAsync(Arg.Any<InviteLinkPolicyValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkPolicyValidationRequest>(), new TwoFactorRequiredForMembership()));

    private static ConfirmOrganizationInviteLinkValidationRequest SetupHappyPath(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser? existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.SupportsConfirmation = true;

        sutProvider.GetDependency<IInviteLinkEligibilityValidator>()
            .Validate(Arg.Any<InviteLinkEligibilityValidationRequest>())
            .Returns(ci => Valid(ci.Arg<InviteLinkEligibilityValidationRequest>()));
        sutProvider.GetDependency<IConfirmInviteLinkMembershipStatusValidator>()
            .Validate(Arg.Any<InviteLinkMembershipStatusValidationRequest>())
            .Returns(ci => Valid(ci.Arg<InviteLinkMembershipStatusValidationRequest>()));
        sutProvider.GetDependency<IInviteLinkFreeOrganizationAdminValidator>()
            .ValidateAsync(Arg.Any<InviteLinkFreeOrganizationAdminValidationRequest>())
            .Returns(ci => Valid(ci.Arg<InviteLinkFreeOrganizationAdminValidationRequest>()));
        sutProvider.GetDependency<IInviteLinkOrganizationCapabilityValidator>()
            .ValidateAsync(Arg.Any<InviteLinkOrganizationCapabilityValidationRequest>())
            .Returns(ci => Valid(ci.Arg<InviteLinkOrganizationCapabilityValidationRequest>()));
        sutProvider.GetDependency<IInviteLinkPolicyValidator>()
            .ValidateAsync(Arg.Any<InviteLinkPolicyValidationRequest>())
            .Returns(ci => Valid(ci.Arg<InviteLinkPolicyValidationRequest>()));

        return new ConfirmOrganizationInviteLinkValidationRequest
        {
            InviteLink = inviteLink,
            Code = Guid.Parse(inviteLink.Code),
            Organization = organization,
            User = user,
            ExistingOrganizationUser = existingOrganizationUser,
        };
    }
}
