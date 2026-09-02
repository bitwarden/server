using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

[SutProviderCustomize]
public class InviteStagedOrganizationUsersCommandTests
{
    private static SutProvider<InviteStagedOrganizationUsersCommand> GetSutProvider() =>
        new SutProvider<InviteStagedOrganizationUsersCommand>()
            .WithFakeTimeProvider()
            .Create();

    /// <summary>
    /// Wires the repositories so every member of <paramref name="organizationUsers"/> is a staged member of
    /// <paramref name="organization"/>, with room for the requested seats.
    /// </summary>
    private static InviteStagedOrganizationUsersRequest Arrange(
        SutProvider<InviteStagedOrganizationUsersCommand> sutProvider,
        Organization organization,
        ICollection<OrganizationUser> organizationUsers,
        Guid performedBy,
        int? seats = 20,
        int occupiedSeats = 1,
        bool useSecretsManager = false,
        bool membersAccessSecretsManager = false)
    {
        organization.Seats = seats;
        organization.UseSecretsManager = useSecretsManager;

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.Status = OrganizationUserStatusType.Staged;
            organizationUser.UserId = null;
            organizationUser.AccessSecretsManager = membersAccessSecretsManager;
        }

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = occupiedSeats, Sponsored = 0 });
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(organizationUsers);

        return new InviteStagedOrganizationUsersRequest
        {
            OrganizationId = organization.Id,
            OrganizationUserIds = organizationUsers.Select(organizationUser => organizationUser.Id).ToList(),
            PerformedBy = performedBy
        };
    }

    /// <summary>
    /// Extends <see cref="Arrange"/> with the stubs a batch needs when every member carries Secrets Manager
    /// access and the subscription has to grow to fit them.
    /// </summary>
    private static InviteStagedOrganizationUsersRequest ArrangeWithSecretsManager(
        SutProvider<InviteStagedOrganizationUsersCommand> sutProvider,
        Organization organization,
        ICollection<OrganizationUser> organizationUsers,
        Guid performedBy,
        int? seats = 20,
        int occupiedSeats = 1)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.SmSeats = 5;
        organization.MaxAutoscaleSeats = 100;

        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, seats, occupiedSeats,
            useSecretsManager: true, membersAccessSecretsManager: true);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, organizationUsers.Count)
            .Returns(organizationUsers.Count);

        return request;
    }

    /// <summary>Makes the invitation email send fail, which is what forces the command to unwind.</summary>
    private static void FailInviteSend(SutProvider<InviteStagedOrganizationUsersCommand> sutProvider) =>
        sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>())
            .ThrowsAsync(new InvalidOperationException("SMTP is down."));

    [Theory, BitAutoData]
    public async Task RunAsync_BumpsRevisionDateAndPreservesIdentityFields(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);

        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);

        var originalIds = organizationUsers.Select(organizationUser => organizationUser.Id).ToList();
        var originalExternalIds = organizationUsers.Select(organizationUser => organizationUser.ExternalId).ToList();
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.RevisionDate = now.UtcDateTime.AddDays(-5);
        }

        var result = await sutProvider.Sut.RunAsync(request);

        // Rows are updated in place: SCIM and Directory Connector key off Id and ExternalId.
        Assert.Equal(originalIds, organizationUsers.Select(organizationUser => organizationUser.Id));
        Assert.Equal(originalExternalIds, organizationUsers.Select(organizationUser => organizationUser.ExternalId));
        Assert.All(organizationUsers, organizationUser =>
        {
            Assert.Equal(now.UtcDateTime, organizationUser.RevisionDate);
            Assert.Null(organizationUser.UserId);
        });

        Assert.Equal(originalIds, result.AsSuccess.Select(memberResult => memberResult.Id));
        Assert.All(result.AsSuccess, memberResult => Assert.True(memberResult.Result.IsSuccess));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_SendsOneInviteBatchFromTheRequestingAdmin(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);

        await sutProvider.Sut.RunAsync(request);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(sendRequest =>
                sendRequest.Organization.Id == organization.Id &&
                sendRequest.InvitingUserId == performedBy &&
                !sendRequest.InitOrganization &&
                sendRequest.Users.Length == organizationUsers.Count));
    }

    [Theory, BitAutoData]
    public async Task RunAsync_LogsInvitedEventPerMember(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);

        await sutProvider.Sut.RunAsync(request);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Count() == organizationUsers.Count &&
                events.All(loggedEvent => loggedEvent.Item2 == EventType.OrganizationUser_Invited)));
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSeatsAvailable_DoesNotAutoscale(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, seats: 20, occupiedSeats: 1);

        await sutProvider.Sut.RunAsync(request);

        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_AutoscalesByTheNumberOfSeatsShort(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        organization.MaxAutoscaleSeats = 100;
        // 10 seats, 9 occupied, so all but one of the batch needs a new seat.
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, seats: 10, occupiedSeats: 9);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AutoAddSeatsAsync(organization, organizationUsers.Count - 1);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenAutoscaleFails_ReturnsErrorAndChangesNothing(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        organization.MaxAutoscaleSeats = 100;
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, seats: 10, occupiedSeats: 10);

        sutProvider.GetDependency<IOrganizationService>()
            .AutoAddSeatsAsync(organization, Arg.Any<int>())
            .ThrowsAsync(new BadRequestException("Seat limit has been reached. Please contact your provider."));

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        // The subscription's own reason reaches the admin; a provider-managed organization has to be told
        // to talk to its provider, not to check its own billing page.
        Assert.Equal("Seat limit has been reached. Please contact your provider.", result.AsError.Message);
        await AssertNothingHappenedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenAutoscaleThrowsInfrastructureFailure_Propagates(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        organization.MaxAutoscaleSeats = 100;
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, seats: 10, occupiedSeats: 10);

        sutProvider.GetDependency<IOrganizationService>()
            .AutoAddSeatsAsync(organization, Arg.Any<int>())
            .ThrowsAsync(new TimeoutException("Stripe is unreachable."));

        await Assert.ThrowsAsync<TimeoutException>(() => sutProvider.Sut.RunAsync(request));
        await AssertNothingHappenedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSendingInvitesFails_RevertsEveryRowAndRethrows(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);

        var originalRevisionDates = organizationUsers
            .ToDictionary(organizationUser => organizationUser.Id, organizationUser => organizationUser.RevisionDate);

        FailInviteSend(sutProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(request));

        Assert.All(organizationUsers, organizationUser =>
        {
            Assert.Equal(OrganizationUserStatusType.Staged, organizationUser.Status);
            Assert.Equal(originalRevisionDates[organizationUser.Id], organizationUser.RevisionDate);
        });

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventsAsync(default(IEnumerable<(OrganizationUser, EventType, DateTime?)>)!);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenAMemberIsMissing_SkipsItAndInvitesTheRest(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy, Guid missingId)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy) with
        {
            OrganizationUserIds = organizationUsers
                .Select(organizationUser => organizationUser.Id)
                .Append(missingId)
                .ToList()
        };

        var result = await sutProvider.Sut.RunAsync(request);

        AssertSkipped<StagedOrganizationUserNotFound>(result, missingId);
        AssertInvited(result, organizationUsers);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenAMemberBelongsToAnotherOrganization_SkipsItAndInvitesTheRest(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy, Guid otherOrganizationId)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);
        var foreignUser = organizationUsers[^1];
        foreignUser.OrganizationId = otherOrganizationId;

        var result = await sutProvider.Sut.RunAsync(request);

        AssertSkipped<StagedOrganizationUserNotFound>(result, foreignUser.Id);
        AssertInvited(result, organizationUsers.Except([foreignUser]).ToList());
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public async Task RunAsync_WhenAMemberIsNoLongerStaged_SkipsItAndInvitesTheRest(
        OrganizationUserStatusType status,
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);
        var promotedUser = organizationUsers[^1];
        promotedUser.Status = status;

        var result = await sutProvider.Sut.RunAsync(request);

        AssertSkipped<OrganizationUserNotStaged>(result, promotedUser.Id);
        AssertInvited(result, organizationUsers.Except([promotedUser]).ToList());
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public async Task RunAsync_WhenNoMemberIsStaged_ReportsEveryRowAndChangesNothing(
        OrganizationUserStatusType status,
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.Status = status;
        }

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        Assert.All(result.AsSuccess, memberResult =>
            Assert.IsType<OrganizationUserNotStaged>(memberResult.Result.AsError));
        await AssertNothingHappenedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenOrganizationDoesNotExist_ReturnsNotFound(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns((Organization?)null);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotFound>(result.AsError);
        await AssertNothingHappenedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenOrganizationHasUnlimitedSeats_SkipsSeatChecks(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, seats: null);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenMembersCarrySecretsManagerAccess_ReservesSecretsManagerSeats(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.SmSeats = 5;
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy,
            useSecretsManager: true, membersAccessSecretsManager: true);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, organizationUsers.Count)
            .Returns(organizationUsers.Count);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        // A staged row occupies no Secrets Manager seat, so promotion buys one for each member that has access.
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(1)
            .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update =>
                update.SmSeats == 5 + organizationUsers.Count && update.Autoscaling));
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenNoMemberCarriesSecretsManagerAccess_LeavesTheSubscriptionAlone(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, useSecretsManager: true);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .DidNotReceiveWithAnyArgs()
            .CountNewSmSeatsRequiredAsync(default, default);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(default!);
    }

    /// <summary>
    /// The flag lives on the row, not the request, so a stale one must not fail an invitation nobody asked
    /// to include Secrets Manager in.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RunAsync_WhenOrganizationNoLongerUsesSecretsManager_IgnoresTheFlagOnTheRow(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy,
            useSecretsManager: false, membersAccessSecretsManager: true);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .DidNotReceiveWithAnyArgs()
            .CountNewSmSeatsRequiredAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSecretsManagerSeatsCannotBeAdded_ReturnsErrorAndChangesNothing(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        organization.PlanType = PlanType.EnterpriseAnnually;
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy,
            useSecretsManager: true, membersAccessSecretsManager: true);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, organizationUsers.Count)
            .Returns(organizationUsers.Count);
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .ThrowsAsync(new BadRequestException("Secrets Manager seat limit reached."));

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.Equal("Secrets Manager seat limit reached.", result.AsError.Message);
        Assert.All(organizationUsers, organizationUser =>
            Assert.Equal(OrganizationUserStatusType.Staged, organizationUser.Status));
        await AssertNothingHappenedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSecretsManagerSeatsCannotBeValidated_BuysNoPasswordManagerSeats(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        // 10 seats, 9 occupied, so all but one of the batch needs a new seat.
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy,
            seats: 10, occupiedSeats: 9);

        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .ValidateUpdateAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .ThrowsAsync(new BadRequestException("Secrets Manager seat limit has been reached."));

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<SecretsManagerSeatExpansionFailed>(result.AsError);
        Assert.Equal("Secrets Manager seat limit has been reached.", result.AsError.Message);

        // The whole point of the dry run: nothing is charged, so there is nothing to hand back.
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AutoAddSeatsAsync(default!, default);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(default!);
        await AssertNothingHappenedAsync(sutProvider);
    }

    /// <summary>
    /// The subscription rejects more Secrets Manager seats than Password Manager seats, so validating against
    /// the organization's current seat total would fail a batch that the autoscale below makes room for.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RunAsync_ValidatesSecretsManagerSeatsAgainstThePostAutoscaleSeatTotal(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy,
            seats: 10, occupiedSeats: 9);

        int? seatsSeenByValidation = null;
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .When(command => command.ValidateUpdateAsync(Arg.Any<SecretsManagerSubscriptionUpdate>()))
            .Do(callInfo => seatsSeenByValidation =
                callInfo.Arg<SecretsManagerSubscriptionUpdate>().Organization.Seats);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(10 + organizationUsers.Count - 1, seatsSeenByValidation);

        // The projection is scratch state. AutoAddSeatsAsync adds its own adjustment on top of Seats, so
        // leaving it in place would buy the batch twice over.
        Assert.Equal(10, organization.Seats);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AutoAddSeatsAsync(organization, organizationUsers.Count - 1);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSecretsManagerUpdateFailsAfterAutoscaling_ReleasesThePasswordManagerSeats(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy,
            seats: 10, occupiedSeats: 9);

        // Validation passes, so this stands in for a late failure at the gateway.
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .ThrowsAsync(new GatewayException("Stripe rejected the subscription change."));

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<SecretsManagerSeatExpansionFailed>(result.AsError);

        // Nobody was invited, so the organization must not keep paying for the seats it just bought.
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AdjustSeatsAsync(organization.Id, -(organizationUsers.Count - 1));
        Assert.All(organizationUsers, organizationUser =>
            Assert.Equal(OrganizationUserStatusType.Staged, organizationUser.Status));
    }

    /// <summary>
    /// The admin needs to see why the invitation failed. A rollback that fails on top of it is an operations
    /// problem, reported through logs rather than by replacing the original error.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RunAsync_WhenReleasingSeatsFails_StillReturnsTheSecretsManagerError(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy,
            seats: 10, occupiedSeats: 9);

        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .ThrowsAsync(new BadRequestException("Secrets Manager seat limit has been reached."));
        sutProvider.GetDependency<IOrganizationService>()
            .AdjustSeatsAsync(organization.Id, Arg.Any<int>())
            .ThrowsAsync(new GatewayException("Stripe is unreachable."));

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.Equal("Secrets Manager seat limit has been reached.", result.AsError.Message);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenNoSecretsManagerSeatsAreNeeded_SkipsValidation(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy);
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, organizationUsers.Count)
            .Returns(0);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .ValidateUpdateAsync(default!);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSendingInvitesFails_ReleasesBothSubscriptionsAndRethrows(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy,
            seats: 10, occupiedSeats: 9);

        // The real command writes the new total onto the entity, so mirror that to prove the rollback restores
        // the seat count captured beforehand rather than the grown one.
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .When(command => command.UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>()))
            .Do(callInfo => organization.SmSeats = callInfo.Arg<SecretsManagerSubscriptionUpdate>().SmSeats);

        FailInviteSend(sutProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(request));

        // Secrets Manager first, so its seat count never exceeds Password Manager's part way through.
        Received.InOrder(() =>
        {
            sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
                .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update =>
                    update.SmSeats == 5 && !update.Autoscaling));
            sutProvider.GetDependency<IOrganizationService>()
                .AdjustSeatsAsync(organization.Id, -(organizationUsers.Count - 1));
        });

        Assert.All(organizationUsers, organizationUser =>
            Assert.Equal(OrganizationUserStatusType.Staged, organizationUser.Status));
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSendingInvitesFailsWithoutSecretsManager_ReleasesOnlyPasswordManagerSeats(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        organization.MaxAutoscaleSeats = 100;
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy, seats: 10, occupiedSeats: 9);

        FailInviteSend(sutProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(request));

        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AdjustSeatsAsync(organization.Id, -(organizationUsers.Count - 1));
        // Nothing was bought there, so there is nothing to give back.
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(default!);
    }

    /// <summary>
    /// The send failure is what the admin needs to see. A rollback that fails on top of it is an operations
    /// problem, reported through logs rather than by replacing the original exception.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RunAsync_WhenReleasingSecretsManagerSeatsFails_StillRethrowsTheSendFailure(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy,
            seats: 10, occupiedSeats: 9);

        FailInviteSend(sutProvider);
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update => !update.Autoscaling))
            .ThrowsAsync(new GatewayException("Stripe is unreachable."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(request));

        // A failed Secrets Manager release must not stop the Password Manager seats coming back.
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .AdjustSeatsAsync(organization.Id, -(organizationUsers.Count - 1));
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenSendingInvitesSucceeds_KeepsBothSubscriptions(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy,
            seats: 10, occupiedSeats: 9);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AdjustSeatsAsync(default, default);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceive()
            .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update => !update.Autoscaling));
    }

    /// <summary>
    /// The discount entitles the whole organization, so promotion must grant it the way the invite, invite-link,
    /// and import paths do rather than honouring whatever was provisioned onto the row.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RunAsync_WhenOrganizationHasSecretsManagerStandalone_GrantsAccessAndBuysThoseSeats(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.SmSeats = 5;
        // Provisioned without Secrets Manager access; the discount grants it anyway.
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy,
            useSecretsManager: true, membersAccessSecretsManager: false);

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));
        sutProvider.GetDependency<IStripePaymentService>()
            .HasSecretsManagerStandalone(organization)
            .Returns(true);
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, organizationUsers.Count)
            .Returns(organizationUsers.Count);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        Assert.All(organizationUsers, organizationUser => Assert.True(organizationUser.AccessSecretsManager));
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(1)
            .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update =>
                update.SmSeats == 5 + organizationUsers.Count && update.Autoscaling));
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenOrganizationHasNoSecretsManagerStandalone_LeavesProvisionedAccessAlone(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = ArrangeWithSecretsManager(sutProvider, organization, organizationUsers, performedBy);
        sutProvider.GetDependency<IStripePaymentService>()
            .HasSecretsManagerStandalone(organization)
            .Returns(false);

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsSuccess);
        // The discount grants access, it never revokes what was provisioned.
        Assert.All(organizationUsers, organizationUser => Assert.True(organizationUser.AccessSecretsManager));
    }

    /// <summary>Asserts the request succeeded overall but reported <typeparamref name="TError"/> for one member.</summary>
    private static void AssertSkipped<TError>(
        CommandResult<ICollection<BulkCommandResult>> result, Guid skippedId) where TError : Error
    {
        Assert.True(result.IsSuccess);
        var skipped = Assert.Single(result.AsSuccess, memberResult => memberResult.Id == skippedId);
        Assert.IsType<TError>(skipped.Result.AsError);
    }

    /// <summary>Asserts every member in <paramref name="expected"/> was invited and reported without an error.</summary>
    private static void AssertInvited(
        CommandResult<ICollection<BulkCommandResult>> result, ICollection<OrganizationUser> expected)
    {
        Assert.All(expected, organizationUser =>
        {
            Assert.Equal(OrganizationUserStatusType.Invited, organizationUser.Status);
            var invited = Assert.Single(result.AsSuccess, memberResult => memberResult.Id == organizationUser.Id);
            Assert.True(invited.Result.IsSuccess);
        });
    }

    private static async Task AssertNothingHappenedAsync(SutProvider<InviteStagedOrganizationUsersCommand> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default!);
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .DidNotReceiveWithAnyArgs()
            .SendInvitesAsync(default!);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventsAsync(default(IEnumerable<(OrganizationUser, EventType, DateTime?)>)!);
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceiveWithAnyArgs()
            .AdjustSeatsAsync(default, default);
    }
}
