using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class PurchasePrivilegedControlsHandlerTests
{
    private const string _representativeMessage =
        "Work with your customer service representative to purchase Privileged Controls.";

    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripePaymentService _paymentService = Substitute.For<IStripePaymentService>();
    private readonly FakeAddPrivilegedControlsCommand _command = new();
    private readonly PurchasePrivilegedControlsHandler _sut;

    public PurchasePrivilegedControlsHandlerTests() =>
        _sut = new PurchasePrivilegedControlsHandler(_organizationRepository, _pricingClient, _paymentService, _command);

    [Fact]
    public async Task HandleAsync_OrganizationMissing_ThrowsNotFound()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns((OrganizationEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(organizationId, Request()));
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrial_ThrowsBadRequestWithRepresentativeMessage()
    {
        var organization = Arrange(CreateOrganization(usePam: true));
        _paymentService.GetSubscriptionAsync(organization).Returns(SubscriptionInfoWithPamLine(false));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal(_representativeMessage, exception.Message);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrialWithNoGatewaySubscriptionId_ThrowsRepresentativeMessageNotNoSubscription()
    {
        var organization = Arrange(CreateOrganization(usePam: true, gatewaySubscriptionId: null));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal(_representativeMessage, exception.Message);
        await _paymentService.DidNotReceive().GetSubscriptionAsync(Arg.Any<ISubscriber>());
    }

    [Fact]
    public async Task HandleAsync_PlanDoesNotSupportPrivilegedControls_ThrowsBadRequestWithoutCallingCommand()
    {
        var organization = CreateOrganization(planType: PlanType.TeamsAnnually);
        Arrange(organization, new TestPlan(ProductTierType.Teams, supportsPrivilegedControls: false));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_NoGatewayCustomerId_ThrowsBadRequestNoPaymentMethodFound()
    {
        var organization = Arrange(CreateOrganization(gatewayCustomerId: null));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal("No payment method found.", exception.Message);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_NoGatewaySubscriptionId_ThrowsBadRequestNoSubscriptionFound()
    {
        var organization = Arrange(CreateOrganization(gatewaySubscriptionId: null));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal("No subscription found.", exception.Message);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_ValidOrganization_CallsCommandWithSeatsAndAutoscaleLimit()
    {
        var organization = Arrange(CreateOrganization());

        await _sut.HandleAsync(organization.Id, Request(seats: 10, maxAutoscaleSeats: 20));

        Assert.Same(organization, _command.ReceivedOrganization);
        Assert.Equal(10, _command.ReceivedSeats);
        Assert.Equal(20, _command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task HandleAsync_CommandThrowsBadRequest_PropagatesUnchanged()
    {
        var organization = Arrange(CreateOrganization());
        var thrown = new BadRequestException("Privileged Controls requires at least 10 seats.");
        _command.ExceptionToThrow = thrown;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request(seats: 3)));

        Assert.Same(thrown, exception);
    }

    [Fact]
    public async Task HandleAsync_CardError_ThrowsBadRequestWithStripeMessage()
    {
        var organization = Arrange(CreateOrganization());
        _command.ExceptionToThrow = new StripeException
        {
            StripeError = new StripeError { Type = "card_error", Message = "Your card was declined." }
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal("Your card was declined.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_NonCardStripeError_PropagatesStripeException()
    {
        var organization = Arrange(CreateOrganization());
        var thrown = new StripeException { StripeError = new StripeError { Type = "api_error", Message = "Stripe is down." } };
        _command.ExceptionToThrow = thrown;

        var exception = await Assert.ThrowsAsync<StripeException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Same(thrown, exception);
    }

    private OrganizationEntity Arrange(OrganizationEntity organization, PlanFeatures? plan = null)
    {
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType)
            .Returns(plan ?? new TestPlan(ProductTierType.Enterprise, supportsPrivilegedControls: true));
        return organization;
    }

    private static OrganizationEntity CreateOrganization(
        PlanType planType = PlanType.EnterpriseAnnually,
        bool usePam = false,
        string? gatewayCustomerId = "cus_123",
        string? gatewaySubscriptionId = "sub_123") => new()
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            UsePam = usePam,
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId
        };

    private static PurchasePrivilegedControlsRequest Request(int seats = 10, int? maxAutoscaleSeats = null) =>
        new() { Seats = seats, MaxAutoscaleSeats = maxAutoscaleSeats };

    private static SubscriptionInfo SubscriptionInfoWithPamLine(bool hasPamLine) => new()
    {
        Subscription = new SubscriptionInfo.BillingSubscription(null!) { HasPrivilegedControlsSeats = hasPamLine }
    };

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan(ProductTierType productTier, bool supportsPrivilegedControls)
        {
            ProductTier = productTier;
            if (supportsPrivilegedControls)
            {
                PrivilegedControls = new PrivilegedControlsPlanFeatures
                {
                    StripeSeatPlanId = "privileged-controls-enterprise-seat-annually",
                    DefaultSeatMinimum = 10
                };
            }
        }
    }

    private sealed class FakeAddPrivilegedControlsCommand : IAddPrivilegedControlsToExistingSubscriptionCommand
    {
        public bool WasCalled { get; private set; }
        public OrganizationEntity? ReceivedOrganization { get; private set; }
        public int ReceivedSeats { get; private set; }
        public int? ReceivedMaxAutoscaleSeats { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
        {
            WasCalled = true;
            ReceivedOrganization = organization;
            ReceivedSeats = seats;
            ReceivedMaxAutoscaleSeats = maxAutoscaleSeats;
            return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
        }
    }
}
