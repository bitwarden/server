using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IHasPaymentMethodQuery _hasPaymentMethodQuery = Substitute.For<IHasPaymentMethodQuery>();
    private readonly FakeAddPrivilegedControlsCommand _command = new();
    private readonly PurchasePrivilegedControlsHandler _sut;

    public PurchasePrivilegedControlsHandlerTests() =>
        _sut = new PurchasePrivilegedControlsHandler(
            _organizationRepository,
            _pricingClient,
            _stripeAdapter,
            _hasPaymentMethodQuery,
            NullLogger<PurchasePrivilegedControlsHandler>.Instance,
            _command);

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
        ArrangeSubscription(StripeConstants.PurchasableReferences.PasswordManagerSeat);

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
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrialWithSubscriptionMissingInStripe_ThrowsRepresentativeMessage()
    {
        var organization = Arrange(CreateOrganization(usePam: true));
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId!, Arg.Any<SubscriptionGetOptions>())
            .Throws(new StripeException { StripeError = new StripeError { Code = StripeConstants.ErrorCodes.ResourceMissing } });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal(_representativeMessage, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_SubscriptionHasPrivilegedControlsLine_IsNotTreatedAsSalesInitiatedTrial()
    {
        var organization = Arrange(CreateOrganization(usePam: true));
        ArrangeSubscription(StripeConstants.PurchasableReferences.PrivilegedControlsSeat);

        await _sut.HandleAsync(organization.Id, Request());

        Assert.True(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_PlanDoesNotSupportPrivilegedControls_ThrowsBadRequestWithoutCallingCommand()
    {
        var organization = CreateOrganization(planType: PlanType.TeamsAnnually);
        Arrange(organization, new TestPlan(ProductTierType.Teams, supportsPrivilegedControls: false));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal("Privileged Controls is only available on Enterprise organizations.", exception.Message);
        Assert.False(_command.WasCalled);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task HandleAsync_NoPaymentMethod_ThrowsBadRequestAskingForOne()
    {
        var organization = Arrange(CreateOrganization());
        _hasPaymentMethodQuery.Run(organization).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal("Add a payment method to your organization before purchasing Privileged Controls.", exception.Message);
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
    public async Task HandleAsync_ValidOrganizationWithPamOff_DoesNotFetchSubscriptionFromStripe()
    {
        var organization = Arrange(CreateOrganization(usePam: false));

        await _sut.HandleAsync(organization.Id, Request());

        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
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

    private OrganizationEntity Arrange(OrganizationEntity organization, PlanFeatures? plan = null)
    {
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType)
            .Returns(plan ?? new TestPlan(ProductTierType.Enterprise, supportsPrivilegedControls: true));
        _hasPaymentMethodQuery.Run(organization).Returns(true);
        return organization;
    }

    private void ArrangeSubscription(string purchasableReference) =>
        _stripeAdapter.GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>())
            .Returns(new Subscription
            {
                Items = new StripeList<SubscriptionItem>
                {
                    Data =
                    [
                        new SubscriptionItem
                        {
                            Price = new Price
                            {
                                Metadata = new Dictionary<string, string>
                                {
                                    [StripeConstants.MetadataKeys.PurchasableReference] = purchasableReference
                                }
                            }
                        }
                    ]
                }
            });

    private static OrganizationEntity CreateOrganization(
        PlanType planType = PlanType.EnterpriseAnnually,
        bool usePam = false,
        string? gatewaySubscriptionId = "sub_123") => new()
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            UsePam = usePam,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = gatewaySubscriptionId
        };

    private static PurchasePrivilegedControlsRequest Request(int seats = 10, int? maxAutoscaleSeats = null) =>
        new() { Seats = seats, MaxAutoscaleSeats = maxAutoscaleSeats };

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
