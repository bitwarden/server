using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class ChangePrivilegedControlsSeatsCommandTests
{
    private const int _currentSeats = 12;
    private const int _seatMinimum = 10;

    private readonly FakePrivilegedControlsSeatChangeSetFactory _seatChangeSetFactory = new();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IGlobalSettings _globalSettings = Substitute.For<IGlobalSettings>();
    private readonly ChangePrivilegedControlsSeatsCommand _sut;

    public ChangePrivilegedControlsSeatsCommandTests() =>
        _sut = new ChangePrivilegedControlsSeatsCommand(
            _seatChangeSetFactory, _updateOrganizationSubscriptionCommand, _organizationService, _globalSettings);

    [Fact]
    public async Task Run_SelfHosted_ThrowsBadRequest()
    {
        var organization = CreateOrganization();
        _globalSettings.SelfHosted.Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Cannot update subscription on a self-hosted instance.", exception.Message);
        Assert.Empty(_seatChangeSetFactory.Calls);
    }

    [Fact]
    public async Task Run_OrganizationHasNoPrivilegedControls_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: false, pamSeats: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Your organization doesn't have Privileged Controls.", exception.Message);
        Assert.Empty(_seatChangeSetFactory.Calls);
    }

    [Fact]
    public async Task Run_OrganizationUsesPrivilegedControlsWithNoSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: true, pamSeats: 0);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Run_NoGatewayCustomer_ThrowsBadRequest(string? gatewayCustomerId)
    {
        var organization = CreateOrganization(gatewayCustomerId: gatewayCustomerId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("No payment method found.", exception.Message);
        Assert.Empty(_seatChangeSetFactory.Calls);
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
        Assert.Empty(_seatChangeSetFactory.Calls);
    }

    [Fact]
    public async Task Run_AutoscaleLimitBelowSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, 14));

        Assert.Equal("Cannot set max seat autoscaling below the Privileged Controls seat count.", exception.Message);
        Assert.Empty(_seatChangeSetFactory.Calls);
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

        var call = Assert.Single(_seatChangeSetFactory.Calls);
        Assert.Same(organization, call.Organization);
        Assert.Equal(15, call.Seats);
    }

    [Fact]
    public async Task Run_SeatIncrease_AppliesTheChangeSetFromTheSeatChangeStep()
    {
        var organization = CreateOrganization();
        var seatChange = SucceedWith();

        await _sut.Run(organization, 15, null);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(organization, seatChange.ChangeSet);
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

        Assert.Empty(_seatChangeSetFactory.Calls);
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
    public async Task Run_SeatChange_LeavesTheSeatMinimumAndPrivilegedControlsFlagAlone()
    {
        var organization = CreateOrganization();
        SucceedWith(minimum: 7);

        await _sut.Run(organization, 15, null);

        Assert.Equal(_seatMinimum, organization.PamSeatMinimum);
        Assert.True(organization.UsePam);
    }

    [Fact]
    public async Task Run_SeatChangeStepRejectsTheRequest_ThrowsBadRequestWithItsMessage()
    {
        var organization = CreateOrganization();
        _seatChangeSetFactory.Result = new BadRequest("Privileged Controls requires at least 10 seats.");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 9, null));

        Assert.Equal("Privileged Controls requires at least 10 seats.", exception.Message);
    }

    [Fact]
    public async Task Run_SeatChangeStepRejectsTheRequest_ChangesAndSavesNothing()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 30);
        _seatChangeSetFactory.Result = new BadRequest("Too few seats.");

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 9, 20));

        Assert.Equal(_currentSeats, organization.PamSeats);
        Assert.Equal(30, organization.MaxAutoscalePamSeats);
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<Bit.Core.Enums.EventType?>());
    }

    [Fact]
    public async Task Run_SeatChangeStepReturnsConflict_ThrowsConflict()
    {
        var organization = CreateOrganization();
        _seatChangeSetFactory.Result = new Conflict("Try again later.");

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Try again later.", exception.Message);
    }

    [Fact]
    public async Task Run_SubscriptionUpdateRejected_ThrowsBadRequestWithItsMessage()
    {
        var organization = CreateOrganization();
        var seatChange = SucceedWith();
        _updateOrganizationSubscriptionCommand.Run(organization, seatChange.ChangeSet)
            .Returns(new BadRequest("Your card was declined."));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Your card was declined.", exception.Message);
    }

    [Fact]
    public async Task Run_SubscriptionUpdateFails_LeavesTheOrganizationUntouchedAndUnsaved()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 30);
        var seatChange = SucceedWith();
        _updateOrganizationSubscriptionCommand.Run(organization, seatChange.ChangeSet)
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
        var seatChange = SucceedWith();
        var underlying = new InvalidOperationException("Stripe is down.");
        _updateOrganizationSubscriptionCommand.Run(organization, seatChange.ChangeSet)
            .Returns(new Unhandled(underlying));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Run(organization, 15, null));

        Assert.Same(underlying, exception);
    }

    private PrivilegedControlsSeatChange SucceedWith(int minimum = _seatMinimum)
    {
        var seatChange = new PrivilegedControlsSeatChange(
            new OrganizationSubscriptionChangeSet
            {
                Changes = [new UpdateItemQuantity("privileged-controls-enterprise-seat-annually", 15)]
            },
            minimum);
        _seatChangeSetFactory.Result = seatChange;
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<OrganizationEntity>(), seatChange.ChangeSet, Arg.Any<Subscription?>())
            .Returns(new Subscription());
        return seatChange;
    }

    private static OrganizationEntity CreateOrganization(
        int? seats = 50,
        bool usePam = true,
        int? pamSeats = _currentSeats,
        int? maxAutoscalePamSeats = null,
        string? gatewayCustomerId = "cus_123",
        string? gatewaySubscriptionId = "sub_123") => new()
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            Seats = seats,
            UsePam = usePam,
            PamSeats = pamSeats,
            PamSeatMinimum = _seatMinimum,
            MaxAutoscalePamSeats = maxAutoscalePamSeats,
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId
        };
}
