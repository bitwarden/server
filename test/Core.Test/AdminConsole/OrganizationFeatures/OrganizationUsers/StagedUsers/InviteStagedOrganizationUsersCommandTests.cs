using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
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
        Assert.Equal(originalIds, result.AsSuccess.Select(organizationUser => organizationUser.Id));
        Assert.Equal(originalExternalIds, result.AsSuccess.Select(organizationUser => organizationUser.ExternalId));
        Assert.All(result.AsSuccess, organizationUser =>
        {
            Assert.Equal(now.UtcDateTime, organizationUser.RevisionDate);
            Assert.Null(organizationUser.UserId);
        });

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
            .ThrowsAsync(new BadRequestException("No payment method on file."));

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<SeatExpansionFailed>(result.AsError);
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

        sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>())
            .ThrowsAsync(new InvalidOperationException("SMTP is down."));

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
    public async Task RunAsync_WhenAnyMemberIsMissing_ReturnsNotFoundAndChangesNothing(
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

        Assert.True(result.IsError);
        Assert.IsType<StagedOrganizationUserNotFound>(result.AsError);
        await AssertNothingHappenedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task RunAsync_WhenAnyMemberBelongsToAnotherOrganization_ReturnsNotFound(
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy, Guid otherOrganizationId)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);
        organizationUsers[^1].OrganizationId = otherOrganizationId;

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<StagedOrganizationUserNotFound>(result.AsError);
        await AssertNothingHappenedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public async Task RunAsync_WhenAnyMemberIsNotStaged_ReturnsErrorAndChangesNothing(
        OrganizationUserStatusType status,
        Organization organization, List<OrganizationUser> organizationUsers, Guid performedBy)
    {
        var sutProvider = GetSutProvider();
        var request = Arrange(sutProvider, organization, organizationUsers, performedBy);
        organizationUsers[^1].Status = status;

        var result = await sutProvider.Sut.RunAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserNotStaged>(result.AsError);
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
        Assert.IsType<SecretsManagerSeatExpansionFailed>(result.AsError);
        Assert.All(organizationUsers, organizationUser =>
            Assert.Equal(OrganizationUserStatusType.Staged, organizationUser.Status));
        await AssertNothingHappenedAsync(sutProvider);
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
    }
}
