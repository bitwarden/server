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

public class AddPrivilegedControlsToExistingSubscriptionCommandTests
{
    private readonly IPrivilegedControlsSeatChangeSetFactory _seatChangeSetFactory =
        Substitute.For<IPrivilegedControlsSeatChangeSetFactory>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly AddPrivilegedControlsToExistingSubscriptionCommand _sut;

    public AddPrivilegedControlsToExistingSubscriptionCommandTests() =>
        _sut = new AddPrivilegedControlsToExistingSubscriptionCommand(
            _seatChangeSetFactory, _updateOrganizationSubscriptionCommand, _organizationService, _environment);

    [Fact]
    public async Task Run_SelfHosted_ThrowsBadRequest()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, null));

        Assert.Equal("Cannot update subscription on a self-hosted instance.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
    }

    [Fact]
    public async Task Run_FirstPurchase_AsksTheSeatChangeStepForTheRequestedSeats()
    {
        var organization = CreateOrganization();
        SucceedWith();

        await _sut.Run(organization, 15, 20);

        await _seatChangeSetFactory.Received(1).CreateAsync(organization, 15);
    }

    [Fact]
    public async Task Run_FirstPurchase_AppliesTheChangeSetFromTheSeatChangeStep()
    {
        var organization = CreateOrganization();
        var changeSet = SucceedWith();

        await _sut.Run(organization, 15, 20);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(organization, changeSet);
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
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
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
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
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
        var organization = CreateOrganization();
        _seatChangeSetFactory.CreateAsync(organization, 9).ThrowsAsync(new BadRequestException("Too few seats."));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 9, null));

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
        var organization = CreateOrganization();
        var changeSet = SucceedWith();
        _updateOrganizationSubscriptionCommand.Run(organization, changeSet)
            .Returns(new BadRequest("Your card was declined."));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 15, 20));

        Assert.Null(organization.PamSeats);
        Assert.Null(organization.MaxAutoscalePamSeats);
        Assert.False(organization.UsePam);
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
            Changes = [new AddItem("privileged-controls-enterprise-seat-annually", 15)],
            ChargeImmediately = true
        };
        _seatChangeSetFactory.CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>()).Returns(changeSet);
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<OrganizationEntity>(), changeSet, Arg.Any<Subscription?>())
            .Returns(new Subscription());
        return changeSet;
    }

    private static OrganizationEntity CreateOrganization(
        int? seats = 50,
        int? pamSeats = null,
        bool usePam = false) => new()
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            Seats = seats,
            PamSeats = pamSeats,
            UsePam = usePam
        };
}
