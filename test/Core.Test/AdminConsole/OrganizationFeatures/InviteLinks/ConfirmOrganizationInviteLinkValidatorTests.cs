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
    public async Task ValidateAsync_WithOrganizationNotUsingInviteLinks_ReturnsInviteLinkNotAvailable(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.UseInviteLinks = false;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
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
    public async Task ValidateAsync_WithUnverifiedEmail_ReturnsEmailNotVerified(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkValidator> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        user.EmailVerified = false;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        var error = Assert.IsType<ConfirmEmailNotVerified>(result.AsError);
        Assert.IsAssignableFrom<IValidationError>(error);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationEmailAsync(Arg.Any<Guid>(), Arg.Any<string>());
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
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
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
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        revokedOrganizationUser.RevocationReason = RevocationReason.Manual;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(revokedOrganizationUser);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

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
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

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
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
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
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        existingOrganizationUser.Status = status;
        existingOrganizationUser.RevocationReason = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(existingOrganizationUser, result.AsSuccess.ExistingOrganizationUser);
    }

    [Theory]
    [BitMemberAutoData(nameof(JoinRequirementsErrors))]
    public async Task ValidateAsync_WithJoinRequirementsError_ReturnsMappedValidationError(
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
    public async Task ValidateAsync_WithAccountRecoveryPolicyViolation_ReturnsResetPasswordKeyRequired(
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
        existingOrganizationUser.Type = OrganizationUserType.Admin;
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
                OrganizationUserType.Admin,
                policyEnforcer);
        sutProvider.GetDependency<IJoinOrganizationPolicyValidationService>()
            .Received(1)
            .ValidateAccountRecoveryPolicy(
                organization.Id,
                user,
                OrganizationUserType.Admin,
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
        Assert.Same(inviteLink, result.AsSuccess.InviteLink);
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
