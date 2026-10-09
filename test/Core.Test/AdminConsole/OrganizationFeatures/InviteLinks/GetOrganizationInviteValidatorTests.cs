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
public class GetOrganizationInviteValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenAllChecksPass_ReturnsValidRequest(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
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
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);

        // Act
        await sutProvider.Sut.ValidateAsync(request);

        // Assert
        sutProvider.GetDependency<IInviteLinkEligibilityValidator>().Received(1)
            .Validate(Arg.Is<InviteLinkEligibilityValidationRequest>(r =>
                r.InviteLink == inviteLink && r.Code == request.Code && r.Organization == organization && r.User == user));
        sutProvider.GetDependency<IAcceptInviteLinkMembershipStatusValidator>().Received(1)
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
    public async Task ValidateAsync_WhenEligibilityFails_ReturnsError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        sutProvider.GetDependency<IInviteLinkEligibilityValidator>()
            .Validate(Arg.Any<InviteLinkEligibilityValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkEligibilityValidationRequest>(), new EmailDomainNotAllowed("Org")));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithConfirmationLink_ValidatesMembershipStatusForConfirmation(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        inviteLink.SupportsConfirmation = true;
        SetAcceptMembershipStatusError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithAcceptLink_ValidatesMembershipStatusForAcceptance(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        inviteLink.SupportsConfirmation = false;
        SetConfirmMembershipStatusError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenMembershipStatusFails_ReturnsError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetAcceptMembershipStatusError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<AlreadyOrganizationMember>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenFreeOrganizationAdminFails_ReturnsError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetFreeOrganizationAdminError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OnlyOneFreeOrganizationAdminAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenCapabilityFails_ReturnsError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetCapabilityError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeats>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenPolicyFails_ReturnsError(
        Organization organization, OrganizationInviteLink inviteLink, User user,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        // Arrange
        var request = SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        SetPolicyError(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
    }

    private static void SetAcceptMembershipStatusError(SutProvider<GetOrganizationInviteValidator> sutProvider) =>
        sutProvider.GetDependency<IAcceptInviteLinkMembershipStatusValidator>()
            .Validate(Arg.Any<InviteLinkMembershipStatusValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkMembershipStatusValidationRequest>(), new AlreadyOrganizationMember("Org")));

    private static void SetConfirmMembershipStatusError(SutProvider<GetOrganizationInviteValidator> sutProvider) =>
        sutProvider.GetDependency<IConfirmInviteLinkMembershipStatusValidator>()
            .Validate(Arg.Any<InviteLinkMembershipStatusValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkMembershipStatusValidationRequest>(), new OrganizationAccessRevoked("Org")));

    private static void SetFreeOrganizationAdminError(SutProvider<GetOrganizationInviteValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkFreeOrganizationAdminValidator>()
            .ValidateAsync(Arg.Any<InviteLinkFreeOrganizationAdminValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkFreeOrganizationAdminValidationRequest>(),
                new OnlyOneFreeOrganizationAdminAllowed()));

    private static void SetCapabilityError(SutProvider<GetOrganizationInviteValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkOrganizationCapabilityValidator>()
            .ValidateAsync(Arg.Any<InviteLinkOrganizationCapabilityValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkOrganizationCapabilityValidationRequest>(),
                new OrganizationHasNoAvailableSeats("Org")));

    private static void SetPolicyError(SutProvider<GetOrganizationInviteValidator> sutProvider) =>
        sutProvider.GetDependency<IInviteLinkPolicyValidator>()
            .ValidateAsync(Arg.Any<InviteLinkPolicyValidationRequest>())
            .Returns(ci => Invalid(ci.Arg<InviteLinkPolicyValidationRequest>(), new TwoFactorRequiredForMembership()));

    // Stubs every shared validator to pass and returns a request for the given link, organization, and membership.
    private static OrganizationInviteLinkValidationRequest SetupHappyPath(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser? existingOrganizationUser,
        SutProvider<GetOrganizationInviteValidator> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.SupportsConfirmation = false;

        sutProvider.GetDependency<IInviteLinkEligibilityValidator>()
            .Validate(Arg.Any<InviteLinkEligibilityValidationRequest>())
            .Returns(ci => Valid(ci.Arg<InviteLinkEligibilityValidationRequest>()));
        sutProvider.GetDependency<IAcceptInviteLinkMembershipStatusValidator>()
            .Validate(Arg.Any<InviteLinkMembershipStatusValidationRequest>())
            .Returns(ci => Valid(ci.Arg<InviteLinkMembershipStatusValidationRequest>()));
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

        return new OrganizationInviteLinkValidationRequest
        {
            InviteLink = inviteLink,
            Code = Guid.Parse(inviteLink.Code),
            Organization = organization,
            User = user,
            ExistingOrganizationUser = existingOrganizationUser,
        };
    }
}
