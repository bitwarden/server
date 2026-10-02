using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
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
            .Returns(ci => Task.FromResult(
                Invalid(ci.Arg<AcceptInviteLinkMembershipValidationRequest>(),
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

    // An Invited row already occupies a seat, so accepting it must not consume or expand capacity even when
    // the organization is full. Guards the Staged handling below from over-reaching.
    [Theory, BitAutoData]
    public async Task AcceptAsync_WithExistingEmailInvite_AndOrganizationAtCapacity_DoesNotReserveSeat(
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
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 2;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(invitedOrganizationUser);
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

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou => ou.Status == OrganizationUserStatusType.Accepted));
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
    }

    // A Staged row is excluded from the occupied seat count, so promoting it to Accepted consumes a seat.
    [Theory, BitAutoData]
    public async Task AcceptAsync_WithStagedMembership_AndSeatsAvailable_AcceptsWithoutAutoscaling(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        stagedOrganizationUser.ExternalId = "ext-123";

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
                ou.Id == stagedOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Accepted &&
                ou.UserId == user.Id &&
                ou.Email == null &&
                ou.ExternalId == "ext-123"));
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithStagedMembership_AndOrganizationAtCapacity_AutoAddsSeat(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 5;

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

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AutoAddSeatsAsync(organization, 1);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == stagedOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Accepted));
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithStagedMembership_AndNoSeatsAvailable_ReturnsError(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
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
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
    }

    [Theory]
    [BitAutoData(typeof(BadRequestException))]
    [BitAutoData(typeof(GatewayException))]
    public async Task AcceptAsync_WithStagedMembership_AndAutoAddSeatsBusinessFailure_ReturnsSeatAddFailed(
        Type exceptionType,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 5;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

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
            .ReplaceAsync(Arg.Any<OrganizationUser>());
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
        SetupAutoConfirmPolicy(organization, sutProvider);
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
        SetupAutoConfirmPolicy(organization, sutProvider);

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

        // The command reads the account-recovery policy and acts on the resulting auto-enroll flag.
        SetupAutoEnrollPolicy(organization, sutProvider);

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
        SetupAutoConfirmPolicy(organization, sutProvider);

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
        SetupAutoConfirmPolicy(organization, sutProvider);

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

    // The command reads the Auto-Confirm policy state itself and passes it into the validator; enabling it
    // drives the emergency-access deletion and auto-confirm push side effects.
    private static void SetupAutoConfirmPolicy(
        Organization organization,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
        => sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(new PolicyStatus(organization.Id, PolicyType.AutomaticUserConfirmation) { Enabled = true });

    // The command reads the account-recovery auto-enroll state itself; enabling it drives reset-password enrollment.
    private static void SetupAutoEnrollPolicy(
        Organization organization,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
        => sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.ResetPassword)
            .Returns(new PolicyStatus(organization.Id, PolicyType.ResetPassword)
            {
                Enabled = true,
                Data = "{\"autoEnrollEnabled\": true}"
            });

    // A Staged membership: SCIM/Directory-Connector provisioned, email set, not yet linked to a User.
    // Call after SetupHappyPath, which stubs the email lookup as returning no membership.
    private static void SetupStagedMembership(
        Organization organization,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        stagedOrganizationUser.OrganizationId = organization.Id;
        stagedOrganizationUser.Status = OrganizationUserStatusType.Staged;
        stagedOrganizationUser.Email = user.Email;
        stagedOrganizationUser.UserId = null;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(stagedOrganizationUser);
    }

    private static void SetupHappyPath(
        Organization org,
        OrganizationInviteLink link,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        org.Enabled = true;
        org.UseInviteLinks = true;
        org.UsePolicies = true;
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

        // Auto-Confirm and account-recovery policies are disabled by default; specific tests opt in via
        // SetupAutoConfirmPolicy / SetupAutoEnrollPolicy.
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(org.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(new PolicyStatus(org.Id, PolicyType.AutomaticUserConfirmation));
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(org.Id, PolicyType.ResetPassword)
            .Returns(new PolicyStatus(org.Id, PolicyType.ResetPassword));

        sutProvider.GetDependency<IAcceptInviteLinkMembershipValidator>()
            .ValidateAsync(Arg.Is<AcceptInviteLinkMembershipValidationRequest>(r =>
                r.Organization.Id == org.Id && r.User == user))
            .Returns(ci => Task.FromResult(Valid(ci.Arg<AcceptInviteLinkMembershipValidationRequest>())));

        sutProvider.GetDependency<IStripePaymentService>()
            .HasSecretsManagerStandalone(org)
            .Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(org.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());
    }
}
