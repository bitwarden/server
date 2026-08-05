using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
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
    public async Task AcceptAsync_WithCodeMismatch_ReturnsInviteLinkNotFound(
        AcceptOrganizationInviteLinkRequest request,
        OrganizationInviteLink inviteLink,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        inviteLink.OrganizationId = request.OrganizationId;
        inviteLink.Code = Guid.NewGuid().ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(request.OrganizationId)
            .Returns(inviteLink);

        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithOrganizationNotFound_ReturnsInviteLinkNotFound(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        var code = Guid.NewGuid();
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(inviteLink);

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = code,
            User = user
        };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithOrganizationDisabled_ReturnsInviteLinkNotFound(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        organization.Enabled = false;
        inviteLink.OrganizationId = organization.Id;
        var code = Guid.NewGuid();
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(inviteLink);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = code,
            User = user
        };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithOrganizationNotUsingInviteLinks_ReturnsInviteLinkNotAvailable(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = false;
        inviteLink.OrganizationId = organization.Id;

        SetupHappyPath(organization, inviteLink, user, sutProvider);
        organization.UseInviteLinks = false;

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    // Eligibility and policy validation lives in IAcceptInviteLinkMembershipValidator (covered by its own
    // tests); the command must surface any validator error and persist nothing.
    [Theory, BitAutoData]
    public async Task AcceptAsync_WhenValidatorReturnsError_SurfacesErrorAndDoesNotPersist(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IAcceptInviteLinkMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptInviteLinkMembershipValidationRequest>())
            .Returns(Task.FromResult(
                Invalid(new AcceptInviteLinkMembershipValidationResult(),
                    new TwoFactorRequiredForMembership())));

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<TwoFactorRequiredForMembership>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeats>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
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

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(
                Arg.Is<OrganizationUser>(ou => ou.OrganizationId == organization.Id && ou.UserId == user.Id),
                EventType.OrganizationUser_InviteLinkAccepted);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoEnroll_AndValidKey_EnrollsUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        // The validator determines whether auto-enroll applies; the command acts on the returned flag.
        sutProvider.GetDependency<IAcceptInviteLinkMembershipValidator>()
            .ValidateAsync(Arg.Is<AcceptInviteLinkMembershipValidationRequest>(r =>
                r.Organization.Id == organization.Id && r.User == user))
            .Returns(Task.FromResult(
                Valid(new AcceptInviteLinkMembershipValidationResult { AutoEnrollEnabled = true })));

        var resetPasswordKey = "valid-key-123";
        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };
        var result = await sutProvider.Sut.AcceptAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
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

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

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
        sutProvider.GetDependency<IAcceptInviteLinkMembershipValidator>()
            .ValidateAsync(Arg.Is<AcceptInviteLinkMembershipValidationRequest>(r =>
                r.Organization.Id == organization.Id && r.User == user))
            .Returns(Task.FromResult(
                Valid(new AcceptInviteLinkMembershipValidationResult
                {
                    AutoConfirmPolicyEnabled = true
                })));
    }

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
        link.Code = Guid.NewGuid().ToString();
        link.AllowedDomains = "[\"example.com\"]";
        user.Email = "user@example.com";
        user.EmailVerified = true;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(org.Id)
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

        sutProvider.GetDependency<IAcceptInviteLinkMembershipValidator>()
            .ValidateAsync(Arg.Is<AcceptInviteLinkMembershipValidationRequest>(r =>
                r.Organization.Id == org.Id && r.User == user))
            .Returns(Task.FromResult(
                Valid(new AcceptInviteLinkMembershipValidationResult())));

        sutProvider.GetDependency<IStripePaymentService>()
            .HasSecretsManagerStandalone(org)
            .Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(org.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());
    }
}
