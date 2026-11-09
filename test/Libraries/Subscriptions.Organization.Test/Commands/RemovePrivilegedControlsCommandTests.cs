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
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class RemovePrivilegedControlsCommandTests
{
    private const string _seatPriceId = "privileged-controls-enterprise-seat-annually";

    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly RecordingLogger<RemovePrivilegedControlsCommand> _logger = new();
    private readonly RemovePrivilegedControlsCommand _sut;

    public RemovePrivilegedControlsCommandTests()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan(supportsPrivilegedControls: true));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TestPlan(supportsPrivilegedControls: false));
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>())
            .Returns(new Subscription());
        _sut = new RemovePrivilegedControlsCommand(
            _pricingClient, _updateOrganizationSubscriptionCommand, _organizationService, _environment, _logger);
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
    public async Task Run_NoGatewaySubscription_ThrowsBadRequest(string? gatewaySubscriptionId)
    {
        var organization = CreateOrganization(gatewaySubscriptionId: gatewaySubscriptionId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization));

        Assert.Equal("No subscription found.", exception.Message);
        await AssertNothingChangedOrSaved();
    }

    [Fact]
    public async Task Run_PlanWithoutPrivilegedControls_LogsOrganizationAndThrowsConflict()
    {
        var organization = CreateOrganization(planType: PlanType.TeamsAnnually);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(organization));

        Assert.Equal(
            "Your organization's subscription is in an invalid state. Please contact support for assistance.",
            exception.Message);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains(organization.Id.ToString(), error);
        Assert.Contains("TeamsAnnually", error);
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

    [Fact]
    public async Task Run_SaveFailsAfterStripeUpdate_LogsBothIdsAndRethrows()
    {
        var organization = CreateOrganization();
        var thrown = new InvalidOperationException("database unavailable");
        _organizationService
            .ReplaceAndUpdateCacheAsync(organization, Arg.Any<Bit.Core.Enums.EventType?>())
            .ThrowsAsync(thrown);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Run(organization));

        Assert.Same(thrown, exception);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains("sub_123", error);
        Assert.Contains(organization.Id.ToString(), error);
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
        string? gatewaySubscriptionId = "sub_123",
        PlanType planType = PlanType.EnterpriseAnnually) => new()
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            UsePam = usePam,
            PamSeats = pamSeats,
            MaxAutoscalePamSeats = maxAutoscalePamSeats,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = gatewaySubscriptionId
        };

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan(bool supportsPrivilegedControls)
        {
            if (supportsPrivilegedControls)
            {
                PrivilegedControls = new PrivilegedControlsPlanFeatures
                {
                    StripeSeatPlanId = _seatPriceId,
                };
            }
        }
    }
}
