using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class AcceptInviteLinkMembershipValidatorTests
{
    public static IEnumerable<object[]> JoinRequirementsErrors() =>
    [
        [new CannotBeAdminOfMultipleFreeOrganizations(), typeof(OnlyOneFreeOrganizationAdminAllowed)],
        [new ProviderUsersCannotJoin(), typeof(ProviderUsersCannotAcceptInviteLink)],
        [new TwoFactorRequiredForMembership(), typeof(TwoFactorRequiredForMembership)],
    ];

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEligibilityRuleFails_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateEligibility(
                inviteLink,
                Guid.Parse(inviteLink.Code),
                organization,
                user)
            .Returns(new EmailDomainNotAllowed(organization.Name));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationEmailAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenMembershipStatusRuleFails_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser revokedMembership,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        revokedMembership.Status = OrganizationUserStatusType.Revoked;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(revokedMembership);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationAccessRevoked>(result.AsError);

        await sutProvider.GetDependency<IPreAccessEnforcerQuery>()
            .DidNotReceiveWithAnyArgs()
            .RunAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitMemberAutoData(nameof(JoinRequirementsErrors))]
    public async Task ValidateAsync_WhenJoinRequirementsFail_ReturnsAcceptError(
        Error joinRequirementsError,
        Type expectedErrorType,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateJoinRequirementsAsync(
                Arg.Any<Organization>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns(new JoinRequirementsValidationResult(joinRequirementsError, AutoConfirmPolicyEnabled: false));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType(expectedErrorType, result.AsError);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .DidNotReceiveWithAnyArgs()
            .ValidateAccountRecoveryPolicy(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<string?>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenAccountRecoveryPolicyFails_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAccountRecoveryPolicy(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<string?>())
            .Returns(new AccountRecoveryPolicyValidationResult(ResetPasswordKeyMissing: true, AutoEnrollEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ResetPasswordKeyRequiredError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenRequirementsPass_ReturnsValidatedContext(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateJoinRequirementsAsync(
                Arg.Any<Organization>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns(new JoinRequirementsValidationResult(null, AutoConfirmPolicyEnabled: true));

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAccountRecoveryPolicy(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<string?>())
            .Returns(new AccountRecoveryPolicyValidationResult(ResetPasswordKeyMissing: false, AutoEnrollEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(organization, result.AsSuccess.Organization);
        Assert.Null(result.AsSuccess.ExistingOrganizationUser);
        Assert.True(result.AsSuccess.AutoConfirmPolicyEnabled);
        Assert.True(result.AsSuccess.AccountRecoveryAutoEnrollEnabled);
    }

    private static AcceptInviteLinkMembershipValidationRequest BuildRequest(OrganizationInviteLink inviteLink, User user) =>
        new()
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };

    private static void SetupHappyPath(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptInviteLinkMembershipValidator> sutProvider)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();
        inviteLink.AllowedDomains = "[\"example.com\"]";
        user.Email = "user@example.com";
        user.EmailVerified = true;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(inviteLink);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IInviteLinkValidationService>()
            .ValidateJoinRequirementsAsync(
                Arg.Any<Organization>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>())
            .Returns(new JoinRequirementsValidationResult(null, AutoConfirmPolicyEnabled: false));

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAccountRecoveryPolicy(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<string?>())
            .Returns(new AccountRecoveryPolicyValidationResult(ResetPasswordKeyMissing: false, AutoEnrollEnabled: false));
    }
}
