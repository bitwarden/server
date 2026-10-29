using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
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

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class AcceptOrganizationInviteLinkCommandTests
{
    public static IEnumerable<object[]> SeatExpansionFailures() =>
    [
        [new BadRequestException("seat failure")],
        [new GatewayException("seat failure")],
    ];

    // Membership and policy validation lives in IAcceptInviteLinkMembershipValidator (covered by its own
    // tests); the command must surface any validator error and persist nothing.
    [Theory, BitAutoData]
    public async Task AcceptAsync_WhenValidatorReturnsError_SurfacesErrorAndDoesNotPersist(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        sutProvider.GetDependency<IAcceptInviteLinkMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptInviteLinkMembershipValidationRequest>())
            .Returns(new TwoFactorRequiredForMembership());

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
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
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        invitedOrganizationUser.Status = OrganizationUserStatusType.Invited;
        invitedOrganizationUser.Email = user.Email;
        invitedOrganizationUser.UserId = null;
        invitedOrganizationUser.ExternalId = "ext-123";
        invitedOrganizationUser.InviteReminderDueDate = DateTime.UtcNow;
        invitedOrganizationUser.InviteRemindersSentCount = 1;
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            ExistingOrganizationUser = invitedOrganizationUser,
        });

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == invitedOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Accepted &&
                ou.UserId == user.Id &&
                ou.Email == null &&
                ou.ExternalId == "ext-123" &&
                ou.InviteReminderDueDate == null &&
                ou.InviteRemindersSentCount == null));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
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
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        stagedOrganizationUser.ExternalId = "ext-123";

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
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
        // Arrange
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

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
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
        // Arrange
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

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeats>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_StagedMemberWithPam_AtLimit_JoinsWithoutPam(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        stagedOrganizationUser.AccessPam = true;
        var memberEmail = stagedOrganizationUser.Email!;
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 1)
            .Returns(new PrivilegedControlsSeatReservationResult(new PrivilegedControlsSeatLimitReachedError()));
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var sendSeatNeededEmailCommand = sutProvider.GetDependency<ISendPrivilegedControlsSeatNeededEmailCommand>();

        var result = await sutProvider.Sut.AcceptAsync(BuildRequest(organization, inviteLink, user));

        Assert.True(result.IsSuccess);
        Received.InOrder(() =>
        {
            organizationUserRepository.ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == stagedOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Accepted &&
                !ou.AccessPam));
            sendSeatNeededEmailCommand.RunAsync(organization, memberEmail);
        });
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_StagedMemberWithPam_ReservationFailed_JoinsWithoutPamOrEmail(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        stagedOrganizationUser.AccessPam = true;
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 1)
            .Returns(new PrivilegedControlsSeatReservationResult(
                new PrivilegedControlsSeatReservationUnexpectedError(), new TimeoutException("Stripe is unreachable.")));

        var result = await sutProvider.Sut.AcceptAsync(BuildRequest(organization, inviteLink, user));

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou => ou.Id == stagedOrganizationUser.Id && !ou.AccessPam));
        await sutProvider.GetDependency<ISendPrivilegedControlsSeatNeededEmailCommand>()
            .DidNotReceiveWithAnyArgs()
            .RunAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_StagedMemberWithPam_SeatAvailable_JoinsWithPam(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        stagedOrganizationUser.AccessPam = true;

        var result = await sutProvider.Sut.AcceptAsync(BuildRequest(organization, inviteLink, user));

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>().Received(1).RunAsync(organization, 1);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == stagedOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Accepted &&
                ou.AccessPam));
        await sutProvider.GetDependency<ISendPrivilegedControlsSeatNeededEmailCommand>()
            .DidNotReceiveWithAnyArgs()
            .RunAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_StagedMemberWithoutPam_ReservesNoPamSeat(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupStagedMembership(organization, user, stagedOrganizationUser, sutProvider);
        stagedOrganizationUser.AccessPam = false;

        var result = await sutProvider.Sut.AcceptAsync(BuildRequest(organization, inviteLink, user));

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>().Received(1).RunAsync(organization, 0);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_NotStaged_DoesNotReservePamSeat(
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
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(organization.Id, user.Email)
            .Returns(invitedOrganizationUser);

        var result = await sutProvider.Sut.AcceptAsync(BuildRequest(organization, inviteLink, user));

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs()
            .RunAsync(default!, default);
    }

    private static AcceptOrganizationInviteLinkRequest BuildRequest(
        Organization organization, OrganizationInviteLink inviteLink, User user) =>
        new()
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoConfirmPolicy_AndNoSeatsAvailable_ReturnsErrorAndDoesNotPersist(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            AutoConfirmPolicyEnabled = true,
        });

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

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeats>(result.AsError);
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
    }

    [Theory]
    [BitMemberAutoData(nameof(SeatExpansionFailures))]
    public async Task AcceptAsync_WithAutoAddSeats_BusinessFailure_ReturnsSeatAddFailed(
        Exception businessFailure,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            AutoConfirmPolicyEnabled = true,
        });
        organization.Seats = 1;
        organization.MaxAutoscaleSeats = 5;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        sutProvider.GetDependency<IOrganizationService>()
            .AutoAddSeatsAsync(organization, 1)
            .Throws(businessFailure);

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<SeatAddFailed>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoAddSeats_UnhandledException_Propagates(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
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

        // Act & Assert
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
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            AutoConfirmPolicyEnabled = true,
        });

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

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
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

        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .Received(1)
            .DeleteAllByUserIdAsync(user.Id);

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
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            AccountRecoveryAutoEnrollEnabled = true,
        });

        var resetPasswordKey = "valid-key-123";
        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
            ResetPasswordKey = resetPasswordKey
        };

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
            .Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(
                organization.Id, user.Id, resetPasswordKey, user.Id);
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithPoliciesDisabled_SkipsPolicySideEffects(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

        // Act
        var result = await sutProvider.Sut.AcceptAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPushAutoConfirmNotificationCommand>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task AcceptAsync_WithAutoConfirmPolicy_EmergencyAccessDeleteThrows_ThrowsWithoutPersisting(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, sutProvider);
        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            AutoConfirmPolicyEnabled = true,
        });

        sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DeleteAllByUserIdAsync(user.Id)
            .Throws(new InvalidOperationException("db failure"));

        var request = new AcceptOrganizationInviteLinkRequest
        {
            OrganizationId = organization.Id,
            Code = Guid.Parse(inviteLink.Code),
            User = user
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.AcceptAsync(request));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
    }

    private static void SetupValidationResult(
        SutProvider<AcceptOrganizationInviteLinkCommand> sutProvider,
        AcceptInviteLinkMembershipValidationResult validationResult)
        => sutProvider.GetDependency<IAcceptInviteLinkMembershipValidator>()
            .ValidateAsync(Arg.Any<AcceptInviteLinkMembershipValidationRequest>())
            .Returns(validationResult);

    // A Staged membership: SCIM/Directory-Connector provisioned, email set, not yet linked to a User.
    // Call after SetupHappyPath, which stubs the validator as returning no membership.
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

        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            ExistingOrganizationUser = stagedOrganizationUser,
        });
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

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(org.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        SetupValidationResult(sutProvider, new AcceptInviteLinkMembershipValidationResult { Organization = org });

        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(org, Arg.Any<int>())
            .Returns(new PrivilegedControlsSeatReservationResult());

        sutProvider.GetDependency<IStripePaymentService>()
            .HasSecretsManagerStandalone(org)
            .Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(org.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());
    }
}
