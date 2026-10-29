using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkValidationServiceTests
{
    [Theory, BitAutoData]
    public void ValidateEligibility_WhenAllChecksPass_ReturnsNull(
        SutProvider<InviteLinkValidationService> sutProvider,
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
            organization,
            user);

        // Assert
        Assert.Null(error);
    }

    [Theory, BitAutoData]
    public void ValidateEligibility_WithLinkNotFound_ReturnsInviteLinkNotFound(
        SutProvider<InviteLinkValidationService> sutProvider,
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
        SutProvider<InviteLinkValidationService> sutProvider,
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
        SutProvider<InviteLinkValidationService> sutProvider,
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
        SutProvider<InviteLinkValidationService> sutProvider,
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
        SutProvider<InviteLinkValidationService> sutProvider,
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
        SutProvider<InviteLinkValidationService> sutProvider,
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

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [BitAutoData(true, true)]
    [BitAutoData(true, false)]
    public void ValidateEligibility_WithVerifiedEmail_ReturnsNull(
        bool selfHosted,
        bool enableOpenInviteLinkEmailVerification,
        SutProvider<InviteLinkValidationService> sutProvider,
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
        SutProvider<InviteLinkValidationService> sutProvider,
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
        SutProvider<InviteLinkValidationService> sutProvider,
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

    // Auth's invite link email verification flow (PM-42435) relies on the domain being checked first.
    [Theory, BitAutoData]
    public void ValidateEligibility_WithUnverifiedEmailAndDisallowedDomain_ReturnsEmailDomainNotAllowedFirst(
        SutProvider<InviteLinkValidationService> sutProvider,
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

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenAllChecksPass_ReturnsNoError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        var policyEnforcer = Substitute.For<IPreAccessPolicyEnforcer>();
        List<OrganizationUser> allOrganizationMemberships = [];
        SetupAllChecksPass(user, allOrganizationMemberships, sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.Admin,
            policyEnforcer);

        // Assert
        Assert.Null(result.Error);
        Assert.False(result.AutoConfirmPolicyEnabled);

        var joinOrganizationPolicyValidationService = sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>();

        await sutProvider.GetDependency<IOrganizationUserValidationService>()
            .Received(1)
            .ValidateFreeOrgAdminLimitAsync(
                user.Id,
                organization.PlanType,
                OrganizationUserType.User,
                OrganizationUserType.Admin);
        await joinOrganizationPolicyValidationService
            .Received(1)
            .ValidateAutomaticUserConfirmationPolicyAsync(
                organization.Id,
                user,
                OrganizationUserType.Admin,
                policyEnforcer,
                allOrganizationMemberships);
        await joinOrganizationPolicyValidationService
            .Received(1)
            .ValidateSingleOrganizationPolicyAsync(
                organization.Id,
                user,
                OrganizationUserType.Admin,
                policyEnforcer,
                allOrganizationMemberships);
        await joinOrganizationPolicyValidationService
            .Received(1)
            .ValidateTwoFactorAuthenticationPolicyAsync(
                organization.Id,
                user,
                OrganizationUserType.Admin,
                policyEnforcer);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenFreeAdminLimitReached_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .ValidateFreeOrgAdminLimitAsync(
                Arg.Any<Guid?>(),
                Arg.Any<PlanType>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<OrganizationUserType>())
            .Returns(new CannotBeAdminOfMultipleFreeOrganizations());

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.Admin,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<CannotBeAdminOfMultipleFreeOrganizations>(result.Error);
        Assert.False(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenAutoConfirmPolicyFails_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAutomaticUserConfirmationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new AutomaticUserConfirmationPolicyValidationResult(
                new UserCannotBelongToAnotherOrganization(user.Email),
                AutoConfirmPolicyEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.Error);
        Assert.True(result.AutoConfirmPolicyEnabled);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenSingleOrgPolicyFails_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateSingleOrganizationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new UserIsAMemberOfAnotherOrganization());

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenTwoFactorPolicyFails_ReturnsError(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateTwoFactorAuthenticationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns(new TwoFactorRequiredForMembership());

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.IsType<TwoFactorRequiredForMembership>(result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateJoinRequirementsAsync_WhenAutoConfirmPolicyEnabled_ReturnsAutoConfirmPolicyEnabled(
        Organization organization,
        User user,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        // Arrange
        SetupAllChecksPass(user, [], sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAutomaticUserConfirmationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new AutomaticUserConfirmationPolicyValidationResult(null, AutoConfirmPolicyEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateJoinRequirementsAsync(
            organization,
            user,
            OrganizationUserType.User,
            Substitute.For<IPreAccessPolicyEnforcer>());

        // Assert
        Assert.Null(result.Error);
        Assert.True(result.AutoConfirmPolicyEnabled);
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

    private static void SetupAllChecksPass(
        User user,
        List<OrganizationUser> allOrganizationMemberships,
        SutProvider<InviteLinkValidationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(allOrganizationMemberships);

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .ValidateFreeOrgAdminLimitAsync(
                Arg.Any<Guid?>(),
                Arg.Any<PlanType>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<OrganizationUserType>())
            .Returns((Error?)null);

        var joinOrganizationPolicyValidationService = sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>();

        joinOrganizationPolicyValidationService
            .ValidateAutomaticUserConfirmationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns(new AutomaticUserConfirmationPolicyValidationResult(null, AutoConfirmPolicyEnabled: false));

        joinOrganizationPolicyValidationService
            .ValidateSingleOrganizationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<ICollection<OrganizationUser>>())
            .Returns((Error?)null);

        joinOrganizationPolicyValidationService
            .ValidateTwoFactorAuthenticationPolicyAsync(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns((Error?)null);
    }
}
