using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class AddPrivilegedControlsCommandTests
{
    private const int _seatMinimum = 10;

    private readonly FakePrivilegedControlsSeatChangeSetFactory _seatChangeSetFactory = new();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly AddPrivilegedControlsCommand _sut;

    public AddPrivilegedControlsCommandTests() =>
        _sut = new AddPrivilegedControlsCommand(
            _seatChangeSetFactory, _updateOrganizationSubscriptionCommand, _organizationService);

    [Fact]
    public async Task Run_FirstPurchase_AsksTheSeatChangeStepForTheRequestedSeats()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, 20);

        var call = Assert.Single(_seatChangeSetFactory.Calls);
        Assert.Same(organization, call.Organization);
        Assert.Equal(15, call.Seats);
    }

    [Fact]
    public async Task Run_FirstPurchase_AppliesTheChangeSetFromTheSeatChangeStep()
    {
        var organization = CreateOrganization();
        var seatChange = SucceedWith();

        await _sut.Run(organization, 15, 20);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(organization, seatChange.ChangeSet);
    }

    [Fact]
    public async Task Run_FirstPurchase_SavesSeatsAutoscaleLimitAndTurnsOnPrivilegedControls()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, 20);

        Assert.Equal(15, organization.PamSeats);
        Assert.Equal(20, organization.MaxAutoscalePamSeats);
        Assert.True(organization.UsePam);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_FirstPurchase_SavesTheSeatMinimumFromTheSeatChangeStep()
    {
        var organization = CreateOrganization(pamSeatMinimum: null);
        SucceedWith();

        await _sut.Run(organization, 15, null);

        Assert.Equal(_seatMinimum, organization.PamSeatMinimum);
    }

    [Fact]
    public async Task Run_SavedMinimum_KeepsItWhenTheSeatChangeStepReturnsIt()
    {
        var organization = CreateOrganization(pamSeatMinimum: 6);
        SucceedWith(minimum: 6);

        await _sut.Run(organization, 15, null);

        Assert.Equal(6, organization.PamSeatMinimum);
    }

    [Fact]
    public async Task Run_NoAutoscaleLimit_LeavesAutoscaleUnlimited()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, null);

        Assert.Null(organization.MaxAutoscalePamSeats);
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
    public async Task Run_MoreSeatsThanPasswordManagerSeats_Succeeds()
    {
        var organization = CreateOrganization(seats: 5);
        SucceedWith();

        await _sut.Run(organization, 15, null);

        Assert.Equal(15, organization.PamSeats);
    }

    [Fact]
    public async Task Run_OrganizationAlreadyHasPrivilegedControlsSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 12);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Your organization already has Privileged Controls.", exception.Message);
        Assert.Empty(_seatChangeSetFactory.Calls);
    }

    [Fact]
    public async Task Run_OrganizationAlreadyUsesPrivilegedControls_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: true);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));
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
        var organization = CreateOrganization();
        _seatChangeSetFactory.Result = new BadRequest("Too few seats.");

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 9, null));

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
        var organization = CreateOrganization();
        var seatChange = SucceedWith();
        _updateOrganizationSubscriptionCommand.Run(organization, seatChange.ChangeSet)
            .Returns(new BadRequest("Your card was declined."));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, 20));

        Assert.Null(organization.PamSeats);
        Assert.Null(organization.MaxAutoscalePamSeats);
        Assert.Null(organization.PamSeatMinimum);
        Assert.False(organization.UsePam);
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
                Changes = [new AddItem("privileged-controls-enterprise-seat-annually", 15)],
                ChargeImmediately = true
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
        int? pamSeats = null,
        int? pamSeatMinimum = null,
        bool usePam = false) => new()
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            Seats = seats,
            PamSeats = pamSeats,
            PamSeatMinimum = pamSeatMinimum,
            UsePam = usePam
        };
}
