using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class ConfirmOrganizationInviteLinkValidatorTests
{
    public static IEnumerable<object[]> JoinRequirementsErrors() =>
    [
        [new CannotBeAdminOfMultipleFreeOrganizations(), typeof(ConfirmOnlyOneFreeOrganizationAdminAllowed)],
        [new OtherOrganizationDoesNotAllowOtherMembership("user@example.com"), typeof(ConfirmOtherOrganizationDoesNotAllowOtherMembership)],
        [new UserCannotBelongToAnotherOrganization("user@example.com"), typeof(ConfirmUserCannotBelongToAnotherOrganization)],
        [new ProviderUsersCannotJoin(), typeof(ConfirmProviderUsersCannotAcceptInviteLink)],
        [new UserIsAMemberOfAnotherOrganization(), typeof(ConfirmUserIsAMemberOfAnotherOrganization)],
        [new UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy(), typeof(ConfirmUserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy)],
        [new TwoFactorRequiredForMembership(), typeof(ConfirmTwoFactorRequiredForMembership)],
    ];

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithLinkThatDoesNotSupportConfirmation_ReturnsConfirmationNotSupported(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        inviteLink.SupportsConfirmation = false;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        var error = Assert.IsType<ConfirmInviteLinkConfirmationNotSupported>(result.AsError);
        Assert.IsAssignableFrom<IValidationError>(error);

        // The check must short-circuit before the membership is resolved so no membership can be mutated.
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationEmailAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEligibilityRuleFails_ReturnsMappedValidationError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        sutProvider.GetDependency<IInviteLinkRules>()
            .ValidateEligibility(
                inviteLink,
                Guid.Parse(inviteLink.Code),
                organization,
                user)
            .Returns(new InviteLinkNotAvailable());

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        var error = Assert.IsType<ConfirmInviteLinkNotAvailable>(result.AsError);
        Assert.IsAssignableFrom<IValidationError>(error);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationEmailAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenEmailInviteFailsMembershipStatusRule_ReturnsMappedValidationError(
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
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOrganizationAccessRevoked>(result.AsError);
    }

    [Theory]
    [BitMemberAutoData(nameof(JoinRequirementsErrors))]
    public async Task ValidateAsync_WhenJoinRequirementsFail_ReturnsMappedValidationError(
        Error joinRequirementsError,
        Type expectedErrorType,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
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
        Assert.IsAssignableFrom<IValidationError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenAccountRecoveryPolicyFails_ReturnsMappedValidationError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
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
            .Returns(new AccountRecoveryPolicyValidationResult(new ResetPasswordKeyRequired(), AutoEnrollEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmResetPasswordKeyRequired>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithExistingMembership_ChecksRequirementsWithExistingRole(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        var existingRole = OrganizationUserType.Admin;
        existingOrganizationUser.Type = existingRole;
        existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        existingOrganizationUser.RevocationReason = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        var policyEnforcer = Substitute.For<IPreAccessPolicyEnforcer>();
        sutProvider.GetDependency<IPreAccessEnforcerQuery>()
            .RunAsync(organization.Id)
            .Returns(policyEnforcer);

        var request = BuildRequest(inviteLink, user) with { ResetPasswordKey = "2.resetPasswordKey" };

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IInviteLinkValidationService>()
            .Received(1)
            .ValidateJoinRequirementsAsync(
                organization,
                user,
                existingRole,
                policyEnforcer);
        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .Received(1)
            .ValidateAccountRecoveryPolicy(
                organization.Id,
                user,
                existingRole,
                policyEnforcer,
                request.ResetPasswordKey);
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

        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .ValidateAccountRecoveryPolicy(
                Arg.Any<Guid>(),
                Arg.Any<User>(),
                Arg.Any<OrganizationUserType>(),
                Arg.Any<IPreAccessPolicyEnforcer>(),
                Arg.Any<string?>())
            .Returns(new AccountRecoveryPolicyValidationResult(null, AutoEnrollEnabled: true));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(organization, result.AsSuccess.Organization);
        Assert.Null(result.AsSuccess.ExistingOrganizationUser);
        Assert.True(result.AsSuccess.AccountRecoveryAutoEnrollEnabled);

        await sutProvider.GetDependency<IInviteLinkValidationService>()
            .Received(1)
            .ValidateJoinRequirementsAsync(
                organization,
                user,
                OrganizationUserType.User,
                Arg.Any<IPreAccessPolicyEnforcer>());
    }

    private static ConfirmOrganizationInviteLinkValidationRequest BuildRequest(OrganizationInviteLink inviteLink, User user) =>
        new()
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
        };

    private static void SetupHappyPath(
        Organization org,
        OrganizationInviteLink link,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        org.Enabled = true;
        org.UseInviteLinks = true;
        org.PlanType = PlanType.EnterpriseAnnually;
        link.OrganizationId = org.Id;
        link.Code = Guid.NewGuid().ToString();
        link.AllowedDomains = "[\"example.com\"]";
        link.SupportsConfirmation = true;
        user.Email = "user@example.com";
        user.EmailVerified = true;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(org.Id)
            .Returns(link);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(org.Id)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(org.Id, user.Email)
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
            .Returns(new AccountRecoveryPolicyValidationResult(null, AutoEnrollEnabled: false));
    }
}
