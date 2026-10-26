using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Commands;
using Bitwarden.Server.Sdk.Environment;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class ChangePrivilegedControlsSeatsCommandTests
{
    private const int _currentSeats = 12;
    private readonly IPrivilegedControlsSeatChangeSetFactory _seatChangeSetFactory =
        Substitute.For<IPrivilegedControlsSeatChangeSetFactory>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly ChangePrivilegedControlsSeatsCommand _sut;

    public ChangePrivilegedControlsSeatsCommandTests() =>
        _sut = new ChangePrivilegedControlsSeatsCommand(
            _seatChangeSetFactory, _updateOrganizationSubscriptionCommand, _organizationService, _environment);

    [Fact]
    public async Task Run_SelfHosted_ThrowsBadRequest()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Cannot update subscription on a self-hosted instance.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_OrganizationHasNoPrivilegedControls_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: false, pamSeats: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Your organization doesn't have Privileged Controls.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_OrganizationUsesPrivilegedControlsWithNoSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: true, pamSeats: 0);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));
    }

    [Fact]
    public async Task Run_PrivilegedControlsOffWithSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: false, pamSeats: _currentSeats);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Your organization doesn't have Privileged Controls.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Run_NoGatewaySubscription_ThrowsBadRequest(string? gatewaySubscriptionId)
    {
        var organization = CreateOrganization(gatewaySubscriptionId: gatewaySubscriptionId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("No subscription found.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_AutoscaleLimitBelowSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, 14));

        Assert.Equal("Cannot set max seat autoscaling below the Privileged Controls seat count.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_AutoscaleLimitEqualToSeats_Succeeds()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, 15);

        Assert.Equal(15, organization.MaxAutoscalePamSeats);
    }

    [Fact]
    public async Task Run_NoAutoscaleLimit_RemovesTheExistingLimit()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 20);
        SucceedWith();

        await _sut.Run(organization, 15, null);

        Assert.Null(organization.MaxAutoscalePamSeats);
    }

    [Fact]
    public async Task Run_SeatIncrease_AsksTheSeatChangeStepForTheRequestedSeats()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, null);

        await _seatChangeSetFactory.Received(1).CreateAsync(organization, 15);
    }

    [Fact]
    public async Task Run_SeatIncrease_AppliesTheChangeSetFromTheSeatChangeStep()
    {
        var organization = CreateOrganization();
        var changeSet = SucceedWith();

        await _sut.Run(organization, 15, null);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(organization, changeSet);
    }

    [Fact]
    public async Task Run_SeatIncrease_SavesSeatsAndAutoscaleLimit()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, 20);

        Assert.Equal(15, organization.PamSeats);
        Assert.Equal(20, organization.MaxAutoscalePamSeats);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_SeatReduction_SavesSeatsAndAutoscaleLimit()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 11, 20);

        Assert.Equal(11, organization.PamSeats);
        Assert.Equal(20, organization.MaxAutoscalePamSeats);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_MoreSeatsThanPasswordManagerSeats_Succeeds()
    {
        var organization = CreateOrganization(seats: 5);
        SucceedWith();

        await _sut.Run(organization, 15, null);

        Assert.Equal(15, organization.PamSeats);
    }

    [Fact]
    public async Task Run_SeatsUnchanged_SkipsTheSeatChangeStepAndTheSubscriptionUpdate()
    {
        var organization = CreateOrganization();

        await _sut.Run(organization, _currentSeats, 20);

        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
    }

    [Fact]
    public async Task Run_SeatsUnchanged_SavesTheNewAutoscaleLimit()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 15);

        await _sut.Run(organization, _currentSeats, 20);

        Assert.Equal(_currentSeats, organization.PamSeats);
        Assert.Equal(20, organization.MaxAutoscalePamSeats);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_SeatsUnchangedWithLimitBelowSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization();

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, _currentSeats, _currentSeats - 1));
    }

    [Fact]
    public async Task Run_SeatChange_LeavesThePrivilegedControlsFlagAlone()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, null);

        Assert.True(organization.UsePam);
    }

    [Fact]
    public async Task Run_SeatChangeStepRejectsTheRequest_ThrowsBadRequestWithItsMessage()
    {
        var organization = CreateOrganization();
        _seatChangeSetFactory.CreateAsync(organization, 9)
            .ThrowsAsync(new BadRequestException("Privileged Controls requires at least 10 seats."));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 9, null));

        Assert.Equal("Privileged Controls requires at least 10 seats.", exception.Message);
    }

    [Fact]
    public async Task Run_SeatChangeStepRejectsTheRequest_ChangesAndSavesNothing()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 30);
        _seatChangeSetFactory.CreateAsync(organization, 9).ThrowsAsync(new BadRequestException("Too few seats."));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 9, 20));

        Assert.Equal(_currentSeats, organization.PamSeats);
        Assert.Equal(30, organization.MaxAutoscalePamSeats);
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<Bit.Core.Enums.EventType?>());
    }

    [Fact]
    public async Task Run_SubscriptionUpdateRejected_ThrowsBadRequestWithItsMessage()
    {
        var organization = CreateOrganization();
        var changeSet = SucceedWith();
        _updateOrganizationSubscriptionCommand.Run(organization, changeSet)
            .Returns(new BadRequest("Your card was declined."));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Your card was declined.", exception.Message);
    }

    [Fact]
    public async Task Run_SubscriptionUpdateFails_LeavesTheOrganizationUntouchedAndUnsaved()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 30);
        var changeSet = SucceedWith();
        _updateOrganizationSubscriptionCommand.Run(organization, changeSet)
            .Returns(new BadRequest("Your card was declined."));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, 20));

        Assert.Equal(_currentSeats, organization.PamSeats);
        Assert.Equal(30, organization.MaxAutoscalePamSeats);
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<Bit.Core.Enums.EventType?>());
    }

    [Fact]
    public async Task Run_SubscriptionUpdateHitsAnUnhandledError_RethrowsTheUnderlyingException()
    {
        var organization = CreateOrganization();
        var changeSet = SucceedWith();
        var underlying = new InvalidOperationException("Stripe is down.");
        _updateOrganizationSubscriptionCommand.Run(organization, changeSet)
            .Returns(new Unhandled(underlying));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Run(organization, 15, null));

        Assert.Same(underlying, exception);
    }

    private OrganizationSubscriptionChangeSet SucceedWith()
    {
        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("privileged-controls-enterprise-seat-annually", 15)]
        };
        _seatChangeSetFactory.CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>()).Returns(changeSet);
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<OrganizationEntity>(), changeSet, Arg.Any<Subscription?>())
            .Returns(new Subscription());
        return changeSet;
    }

    private static OrganizationEntity CreateOrganization(
        int? seats = 50,
        bool usePam = true,
        int? pamSeats = _currentSeats,
        int? maxAutoscalePamSeats = null,
        string? gatewaySubscriptionId = "sub_123") => new()
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            Seats = seats,
            UsePam = usePam,
            PamSeats = pamSeats,
            MaxAutoscalePamSeats = maxAutoscalePamSeats,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = gatewaySubscriptionId
        };
}
