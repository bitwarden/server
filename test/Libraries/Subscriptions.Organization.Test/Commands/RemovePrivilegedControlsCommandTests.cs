using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Commands;
using Bitwarden.Server.Sdk.Environment;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class RemovePrivilegedControlsCommandTests
{
    private const int _seatMinimum = 10;
    private const string _seatPriceId = "privileged-controls-enterprise-seat-annually";

    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly RemovePrivilegedControlsCommand _sut;

    public RemovePrivilegedControlsCommandTests()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>())
            .Returns(new Subscription());
        _sut = new RemovePrivilegedControlsCommand(
            _pricingClient, _updateOrganizationSubscriptionCommand, _organizationService, _environment);
    }

    [Fact]
    public async Task Run_SelfHosted_ThrowsBadRequest()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));

        Assert.Equal("Cannot update subscription on a self-hosted instance.", exception.Message);
        await AssertNothingChangedOrSaved();
    }

    [Fact]
    public async Task Run_OrganizationHasNoPrivilegedControls_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: false, pamSeats: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));

        Assert.Equal("Your organization doesn't have Privileged Controls.", exception.Message);
        await AssertNothingChangedOrSaved();
    }

    [Fact]
    public async Task Run_OrganizationUsesPrivilegedControlsWithNoSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: true, pamSeats: 0);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Run_NoGatewayCustomer_ThrowsBadRequest(string? gatewayCustomerId)
    {
        var organization = CreateOrganization(gatewayCustomerId: gatewayCustomerId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));

        Assert.Equal("No payment method found.", exception.Message);
        await AssertNothingChangedOrSaved();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Run_NoGatewaySubscription_ThrowsBadRequest(string? gatewaySubscriptionId)
    {
        var organization = CreateOrganization(gatewaySubscriptionId: gatewaySubscriptionId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));

        Assert.Equal("No subscription found.", exception.Message);
        await AssertNothingChangedOrSaved();
    }

    [Fact]
    public async Task Run_RemovesThePrivilegedControlsSeatItemAndChargesImmediately()
    {
        var organization = CreateOrganization();

        await _sut.Run(organization);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(
            organization,
            Arg.Is<OrganizationSubscriptionChangeSet>(changeSet =>
                changeSet.ChargeImmediately
                && changeSet.Changes.Count == 1
                && changeSet.Changes[0].AsT2.PriceId == _seatPriceId));
    }

    [Fact]
    public async Task Run_TurnsOffPrivilegedControls()
    {
        var organization = CreateOrganization();

        await _sut.Run(organization);

        Assert.False(organization.UsePam);
    }

    [Fact]
    public async Task Run_ClearsTheSeatCountAndAutoscaleLimit()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 20);

        await _sut.Run(organization);

        Assert.Null(organization.PamSeats);
        Assert.Null(organization.MaxAutoscalePamSeats);
    }

    [Fact]
    public async Task Run_LeavesTheSeatMinimumAlone()
    {
        var organization = CreateOrganization();

        await _sut.Run(organization);

        Assert.Equal(_seatMinimum, organization.PamSeatMinimum);
    }

    [Fact]
    public async Task Run_SavesTheOrganization()
    {
        var organization = CreateOrganization();

        await _sut.Run(organization);

        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_SubscriptionUpdateRejected_ThrowsBadRequestWithItsMessage()
    {
        var organization = CreateOrganization();
        _updateOrganizationSubscriptionCommand
            .Run(organization, Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>())
            .Returns(new BadRequest("Your subscription cannot be updated in its current status."));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));

        Assert.Equal("Your subscription cannot be updated in its current status.", exception.Message);
    }

    [Fact]
    public async Task Run_SubscriptionUpdateFails_LeavesTheOrganizationUntouchedAndUnsaved()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 30);
        _updateOrganizationSubscriptionCommand
            .Run(organization, Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>())
            .Returns(new BadRequest("Your subscription cannot be updated in its current status."));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));

        Assert.True(organization.UsePam);
        Assert.Equal(12, organization.PamSeats);
        Assert.Equal(30, organization.MaxAutoscalePamSeats);
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<Bit.Core.Enums.EventType?>());
    }

    private async Task AssertNothingChangedOrSaved()
    {
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<Bit.Core.Enums.EventType?>());
    }

    private static OrganizationEntity CreateOrganization(
        bool usePam = true,
        int? pamSeats = 12,
        int? maxAutoscalePamSeats = null,
        string? gatewayCustomerId = "cus_123",
        string? gatewaySubscriptionId = "sub_123") => new()
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            UsePam = usePam,
            PamSeats = pamSeats,
            PamSeatMinimum = _seatMinimum,
            MaxAutoscalePamSeats = maxAutoscalePamSeats,
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId
        };

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan() =>
            PrivilegedControls = new PrivilegedControlsPlanFeatures
            {
                StripeSeatPlanId = _seatPriceId,
                DefaultSeatMinimum = _seatMinimum
            };
    }
}
