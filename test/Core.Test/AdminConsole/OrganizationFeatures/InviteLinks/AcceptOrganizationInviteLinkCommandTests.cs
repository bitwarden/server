using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class AcceptOrganizationInviteLinkCommandTests
{
    [Theory, BitAutoData]
    public async Task AcceptAsync_WithLinkNotFound_ReturnsInviteLinkNotFound(
        AcceptOrganizationInviteLinkRequest request,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithOrganizationNotFound_ReturnsInviteLinkNotFound(
        AcceptOrganizationInviteLinkRequest request,
        OrganizationInviteLink inviteLink,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(inviteLink.Code)
            .Returns(inviteLink);

        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithOrganizationDisabled_ReturnsInviteLinkNotFound(
        Organization organization,
        OrganizationInviteLink inviteLink,
        AcceptOrganizationInviteLinkRequest request,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        organization.Enabled = false;
        inviteLink.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(inviteLink.Code)
            .Returns(inviteLink);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithOrganizationNotUsingInviteLinks_ReturnsInviteLinkNotAvailable(
        Organization organization,
        OrganizationInviteLink inviteLink,
        AcceptOrganizationInviteLinkRequest request,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = false;
        inviteLink.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(request.Code)
            .Returns(inviteLink);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithEmailDomainNotAllowed_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        inviteLink.AllowedDomains = "[\"allowed.com\"]";
        user.Email = "user@notallowed.com";

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithRevokedMember_ReturnsOrganizationAccessRevoked(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser revokedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        revokedOrganizationUser.Status = OrganizationUserStatusType.Revoked;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(revokedOrganizationUser);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationAccessRevoked>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithRevokedEmailInvite_ReturnsOrganizationAccessRevoked(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser revokedEmailInvite,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        revokedEmailInvite.Status = OrganizationUserStatusType.Revoked;
        revokedEmailInvite.Email = user.Email;
        revokedEmailInvite.UserId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(revokedEmailInvite);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationAccessRevoked>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public async Task AcceptAsync_WithAlreadyMember_ReturnsAlreadyOrganizationMember(
        OrganizationUserStatusType status,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        existingOrganizationUser.Status = status;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(existingOrganizationUser);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<AlreadyOrganizationMember>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithExistingEmailInvite_UpdatesOrganizationUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser invitedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        invitedOrganizationUser.Status = OrganizationUserStatusType.Invited;
        invitedOrganizationUser.Email = user.Email;
        invitedOrganizationUser.UserId = null;
        invitedOrganizationUser.ExternalId = "ext-123";

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(invitedOrganizationUser);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == invitedOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Accepted &&
                ou.UserId == user.Id &&
                ou.Email == null &&
                ou.ExternalId == "ext-123"));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithFreeAdmin_AdminLimitReached_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser invitedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.PlanType = PlanType.Free;
        invitedOrganizationUser.Status = OrganizationUserStatusType.Invited;
        invitedOrganizationUser.Type = OrganizationUserType.Admin;
        invitedOrganizationUser.Email = user.Email;
        invitedOrganizationUser.UserId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(invitedOrganizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(1);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OnlyOneFreeOrganizationAdminAllowed>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithFreeAdmin_NotAtLimit_Succeeds(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser invitedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.PlanType = PlanType.Free;
        invitedOrganizationUser.Status = OrganizationUserStatusType.Invited;
        invitedOrganizationUser.Type = OrganizationUserType.Admin;
        invitedOrganizationUser.Email = user.Email;
        invitedOrganizationUser.UserId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(invitedOrganizationUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(0);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == invitedOrganizationUser.Id &&
                ou.Type == OrganizationUserType.Admin));
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithSingleOrgPolicyViolation_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptOrganizationMembershipValidationRequest>())
            .Returns(Task.FromResult(
                Invalid(new AcceptOrganizationMembershipValidationResult(),
                    new UserIsAMemberOfAnotherOrganization())));

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithTwoFactorPolicy_UserLacks2FA_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptOrganizationMembershipValidationRequest>())
            .Returns(Task.FromResult(
                Invalid(new AcceptOrganizationMembershipValidationResult(),
                    new TwoFactorRequiredForMembership())));

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoEnroll_MissingResetPasswordKey_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<ResetPasswordPolicyRequirement>(user.Id)
            .Returns(new ResetPasswordPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organization.Id,
                    PolicyType = PolicyType.ResetPassword,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                    PolicyData = "{\"autoEnrollEnabled\": true}"
                }
            ]));

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user, ResetPasswordKey = null };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ResetPasswordKeyRequired>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithNoSeatsAvailable_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeats>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoConfirmPolicy_AndNoSeatsAvailable_DoesNotDeleteEa(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupAutoConfirmPolicy(organization, user, sutProvider);
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeats>(result.AsError);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(typeof(BadRequestException))]
    [BitAutoData(typeof(GatewayException))]
    public async Task AcceptAsync_WithAutoAddSeats_BusinessFailure_ReturnsSeatAddFailed(
        Type exceptionType,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.Seats = 1;
        organization.MaxAutoscaleSeats = 5;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        Exception businessFailure = exceptionType == typeof(BadRequestException)
            ? new BadRequestException("seat failure")
            : new GatewayException("seat failure");
        sutProvider.GetDependency<IOrganizationService>()
            .AutoAddSeatsAsync(organization, 1)
            .Throws(businessFailure);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<SeatAddFailed>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoAddSeats_UnhandledException_Propagates(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.Seats = 1;
        organization.MaxAutoscaleSeats = 5;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        sutProvider.GetDependency<IOrganizationService>()
            .AutoAddSeatsAsync(organization, 1)
            .Throws(new InvalidOperationException("stripe outage"));

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.AcceptAsync(request));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithNewMember_CreatesOrganizationUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupAutoConfirmPolicy(organization, user, sutProvider);

        var adminDetails = new OrganizationUserUserDetails { Email = "admin@example.com" };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails> { adminDetails });

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        var organizationUser = result.AsSuccess;
        Assert.Equal(organization.Id, organizationUser.OrganizationId);
        Assert.Equal(user.Id, organizationUser.UserId);
        Assert.Equal(OrganizationUserStatusType.Accepted, organizationUser.Status);
        Assert.Equal(OrganizationUserType.User, organizationUser.Type);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationUser>(ou =>
                ou.OrganizationId == organization.Id &&
                ou.UserId == user.Id &&
                ou.Status == OrganizationUserStatusType.Accepted));

        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationAcceptedEmailAsync(
                organization,
                user.Email,
                Arg.Is<IEnumerable<string>>(emails => emails.Contains(adminDetails.Email)));

        await sutProvider.GetDependency<IPushAutoConfirmNotificationCommand>()
            .Received(1)
            .PushAsync(user.Id, organization.Id);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoEnroll_AndValidKey_EnrollsUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<ResetPasswordPolicyRequirement>(user.Id)
            .Returns(new ResetPasswordPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organization.Id,
                    PolicyType = PolicyType.ResetPassword,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                    PolicyData = "{\"autoEnrollEnabled\": true}"
                }
            ]));

        var resetPasswordKey = "valid-key-123";
        var request = new AcceptOrganizationInviteLinkRequest
        {
            Code = inviteLink.Code,
            User = user,
            ResetPasswordKey = resetPasswordKey
        };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
            .Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(
                organization.Id, user.Id, resetPasswordKey, user.Id);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithNoAutoEnroll_DoesNotEnrollUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoConfirmPolicy_Enabled_DeletesEmergencyAccess(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupAutoConfirmPolicy(organization, user, sutProvider);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .Received(1)
            .DeleteAllByUserIdAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoConfirmPolicy_Disabled_DoesNotDeleteEmergencyAccess(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithProviderUser_ReturnsProviderUsersCannotAcceptInviteLink(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        ProviderUser providerUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([providerUser]);

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ProviderUsersCannotAcceptInviteLink>(result.AsError);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoConfirmPolicy_AndMultiOrgUser_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptOrganizationMembershipValidationRequest>())
            .Returns(Task.FromResult(
                Invalid(new AcceptOrganizationMembershipValidationResult(),
                    new UserCannotBelongToAnotherOrganization())));

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<UserCannotBelongToAnotherOrganization>(result.AsError);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoConfirmPolicy_EaDeleteThrows_ThrowsWithoutPersisting(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupAutoConfirmPolicy(organization, user, sutProvider);

        sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DeleteAllByUserIdAsync(user.Id)
            .Throws(new InvalidOperationException("db failure"));

        var request = new AcceptOrganizationInviteLinkRequest { Code = inviteLink.Code, User = user };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.AcceptAsync(request));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    private static void SetupAutoConfirmPolicy(
        Organization organization,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .ValidateAsync(Arg.Is<AcceptOrganizationMembershipValidationRequest>(r =>
                r.OrganizationId == organization.Id && r.User == user))
            .Returns(Task.FromResult(
                Valid(new AcceptOrganizationMembershipValidationResult
                {
                    AutoConfirmPolicyEnabled = true
                })));
    }

    /// <summary>
    /// Configures the default "happy path" mocks so individual tests only need
    /// to override the one thing they are testing.
    /// </summary>
    private static void SetupHappyPath(
        Organization org,
        OrganizationInviteLink link,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        org.Enabled = true;
        org.UseInviteLinks = true;
        org.Seats = 10;
        org.MaxAutoscaleSeats = null;
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

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(org.Id, user.Email)
            .Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser>());

        // No provider membership by default
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<ResetPasswordPolicyRequirement>(user.Id)
            .Returns(new ResetPasswordPolicyRequirement([]));

        // Membership validator returns valid (no restrictions) by default
        sutProvider.GetDependency<IAcceptOrganizationMembershipValidator>()
            .ValidateAsync(Arg.Is<AcceptOrganizationMembershipValidationRequest>(r =>
                r.OrganizationId == org.Id && r.User == user))
            .Returns(Task.FromResult(
                Valid(new AcceptOrganizationMembershipValidationResult())));

        sutProvider.GetDependency<IStripePaymentService>()
            .HasSecretsManagerStandalone(org)
            .Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(org.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());
    }
}
