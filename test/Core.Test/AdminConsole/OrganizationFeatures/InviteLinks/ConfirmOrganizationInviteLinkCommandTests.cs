using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class ConfirmOrganizationInviteLinkCommandTests
{
    // The two confirmations that consume a seat: a brand-new membership (no existing row), or a Staged row,
    // which is excluded from the occupied seat count.
    public static IEnumerable<object?[]> SeatConsumingMemberships() =>
    [
        [null],
        [new OrganizationUser { Status = OrganizationUserStatusType.Staged }],
    ];

    public static IEnumerable<object?[]> SeatExpansionFailures() =>
        from failure in new Exception[] { new BadRequestException("seat failure"), new GatewayException("seat failure") }
        from membership in SeatConsumingMemberships()
        select new object?[] { failure, membership[0] };

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenValidationFails_ReturnsErrorAndDoesNotWrite(
        ConfirmOrganizationInviteLinkRequest request,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IConfirmOrganizationInviteLinkValidator>()
            .ValidateAsync(Arg.Any<ConfirmOrganizationInviteLinkValidationRequest>())
            .Returns(new InviteLinkNotFound());

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>());
        AssertNoMembershipWritten(sutProvider);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    public async Task ConfirmAsync_WithExistingMembership_ConfirmsDirectlyWithoutCreatingMembership(
        OrganizationUserStatusType status,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        existingOrganizationUser.Status = status;
        organization.Seats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        var request = BuildRequest(inviteLink, user);

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == existingOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Confirmed &&
                ou.UserId == user.Id &&
                ou.Email == null &&
                ou.Key == request.OrgUserKey));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(
                Arg.Is<OrganizationUser>(ou => ou.Id == existingOrganizationUser.Id),
                EventType.OrganizationUser_InviteLinkConfirmed);
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WithNewUser_CreatesMembershipDirectlyInConfirmedStatus(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser: null, sutProvider);
        organization.Seats = null;
        var request = BuildRequest(inviteLink, user);

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationUser>(ou =>
                ou.OrganizationId == organization.Id &&
                ou.UserId == user.Id &&
                ou.Status == OrganizationUserStatusType.Confirmed &&
                ou.Type == OrganizationUserType.User &&
                ou.Key == request.OrgUserKey));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(
                Arg.Is<OrganizationUser>(ou =>
                    ou.OrganizationId == organization.Id &&
                    ou.UserId == user.Id &&
                    ou.Status == OrganizationUserStatusType.Confirmed),
                EventType.OrganizationUser_InviteLinkConfirmed);
    }

    [Theory]
    [BitMemberAutoData(nameof(SeatConsumingMemberships))]
    public async Task ConfirmAsync_WithSeatConsumingMembership_AtCapacity_AutoAddsSeat(
        OrganizationUser? existingOrganizationUser,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupValidatedConfirmation(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 5;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AutoAddSeatsAsync(organization, 1);
    }

    [Theory]
    [BitMemberAutoData(nameof(SeatExpansionFailures))]
    public async Task ConfirmAsync_WithSeatConsumingMembership_SeatExpansionFails_ReturnsErrorAndDoesNotWrite(
        Exception businessFailure,
        OrganizationUser? existingOrganizationUser,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupValidatedConfirmation(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 5;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        sutProvider.GetDependency<IOrganizationService>()
            .AutoAddSeatsAsync(organization, 1)
            .ThrowsAsync(businessFailure);

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmSeatAddFailed>(result.AsError);
        AssertNoMembershipWritten(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(SeatConsumingMemberships))]
    public async Task ConfirmAsync_WithSeatConsumingMembership_NoSeatsAvailable_ReturnsErrorAndDoesNotWrite(
        OrganizationUser? existingOrganizationUser,
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupValidatedConfirmation(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 2, Sponsored = 0 });

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmOrganizationHasNoAvailableSeats>(result.AsError);
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
        AssertNoMembershipWritten(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WithStagedMembership_AndSeatsAvailable_ConfirmsWithoutAutoscaling(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser stagedOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, stagedOrganizationUser, sutProvider);
        SetupStagedMembership(user, stagedOrganizationUser);
        stagedOrganizationUser.ExternalId = "ext-123";
        organization.Seats = 2;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        var request = BuildRequest(inviteLink, user);

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Id == stagedOrganizationUser.Id &&
                ou.Status == OrganizationUserStatusType.Confirmed &&
                ou.UserId == user.Id &&
                ou.Email == null &&
                ou.Key == request.OrgUserKey &&
                ou.ExternalId == stagedOrganizationUser.ExternalId));
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenAutoEnrollEnabledAndKeyMissing_ReturnsResetPasswordKeyRequired(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        SetupAutoEnrollPolicy(organization, user, sutProvider);
        var request = BuildRequest(inviteLink, user) with { ResetPasswordKey = null };

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ConfirmResetPasswordKeyRequired>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenAutoEnrollEnabledWithValidKey_EnrollsUser(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        SetupAutoEnrollPolicy(organization, user, sutProvider);
        var request = BuildRequest(inviteLink, user) with { ResetPasswordKey = "2.validresetpasswordkey" };

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
            .Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(organization.Id, user.Id, request.ResetPasswordKey, user.Id);
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenDataOwnershipApplies_CreatesDefaultCollection(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);
        organization.UseMyItems = true;
        SetupDataOwnershipPolicy(organization, existingOrganizationUser, user, sutProvider);
        var request = BuildRequest(inviteLink, user);

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateDefaultCollectionsAsync(
                organization.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(existingOrganizationUser.Id)),
                request.DefaultUserCollectionName);
    }

    [Theory, BitAutoData]
    public async Task ConfirmAsync_WhenDataOwnershipDoesNotApply_DoesNotCreateDefaultCollection(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        // No data ownership policy details are configured, so the policy does not apply.
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }


    [Theory, BitAutoData]
    public async Task ConfirmAsync_WithExistingMembership_PushesSyncOrgKeys(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        // Arrange
        SetupHappyPath(organization, inviteLink, user, existingOrganizationUser, sutProvider);

        // Act
        var result = await sutProvider.Sut.ConfirmAsync(BuildRequest(inviteLink, user));

        // Assert
        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n => n.Type == PushType.SyncOrgKeys && n.TargetId == user.Id));
    }

    private static ConfirmOrganizationInviteLinkRequest BuildRequest(OrganizationInviteLink inviteLink, User user) =>
        new()
        {
            OrganizationId = inviteLink.OrganizationId,
            Code = Guid.Parse(inviteLink.Code),
            User = user,
            OrgUserKey = "4.orgUserKey",
            DefaultUserCollectionName = "2.defaultCollectionName",
        };

    private static void SetupHappyPath(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser? existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        if (existingOrganizationUser is not null)
        {
            existingOrganizationUser.OrganizationId = organization.Id;
            existingOrganizationUser.UserId = user.Id;
            existingOrganizationUser.Status = OrganizationUserStatusType.Accepted;
        }

        SetupValidatedConfirmation(organization, inviteLink, user, existingOrganizationUser, sutProvider);
    }

    // Stubs a successful validation that resolves to the given membership, leaving the membership untouched.
    private static void SetupValidatedConfirmation(
        Organization organization,
        OrganizationInviteLink inviteLink,
        User user,
        OrganizationUser? existingOrganizationUser,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;
        inviteLink.Code = Guid.NewGuid().ToString();

        sutProvider.GetDependency<IConfirmOrganizationInviteLinkValidator>()
            .ValidateAsync(Arg.Any<ConfirmOrganizationInviteLinkValidationRequest>())
            .Returns(new ConfirmOrganizationInviteLinkValidationResult
            {
                InviteLink = inviteLink,
                Organization = organization,
                ExistingOrganizationUser = existingOrganizationUser,
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 0, Sponsored = 0 });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<ResetPasswordPolicyRequirement>(user.Id)
            .Returns(new ResetPasswordPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(user.Id)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Disabled, []));
    }

    private static void AssertNoMembershipWritten(SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<OrganizationUser>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>());
        sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
        sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<UserPushNotification>>());
    }

    // A Staged membership: SCIM/Directory-Connector provisioned, email set, not yet linked to a User.
    // Call after SetupHappyPath, which defaults the existing membership to Accepted.
    private static void SetupStagedMembership(User user, OrganizationUser stagedOrganizationUser)
    {
        stagedOrganizationUser.Status = OrganizationUserStatusType.Staged;
        stagedOrganizationUser.Email = user.Email;
        stagedOrganizationUser.UserId = null;
    }

    private static void SetupAutoEnrollPolicy(
        Organization organization,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
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
    }

    private static void SetupDataOwnershipPolicy(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<ConfirmOrganizationInviteLinkCommand> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(user.Id)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled,
            [
                new PolicyDetails
                {
                    OrganizationId = organization.Id,
                    OrganizationUserId = organizationUser.Id,
                    PolicyType = PolicyType.OrganizationDataOwnership,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                }
            ]));
    }
}
