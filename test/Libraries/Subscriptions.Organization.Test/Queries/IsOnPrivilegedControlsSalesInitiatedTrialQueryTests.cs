using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Subscriptions.Organization.Queries;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Queries;

public class IsOnPrivilegedControlsSalesInitiatedTrialQueryTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly RecordingLogger<IsOnPrivilegedControlsSalesInitiatedTrialQuery> _logger = new();
    private readonly IsOnPrivilegedControlsSalesInitiatedTrialQuery _sut;

    public IsOnPrivilegedControlsSalesInitiatedTrialQueryTests() =>
        _sut = new IsOnPrivilegedControlsSalesInitiatedTrialQuery(_pricingClient, _stripeAdapter, _logger);

    [Fact]
    public async Task Run_PrivilegedControlsOff_ReturnsFalseWithoutReadingPlanOrSubscription()
    {
        var organization = CreateOrganization(usePam: false);

        Assert.False(await _sut.Run(organization));
        await _pricingClient.DidNotReceiveWithAnyArgs().GetPlanOrThrow(default);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_PlanWithoutPrivilegedControls_ReturnsFalseWithoutReadingSubscription()
    {
        var organization = CreateOrganization(supportsPrivilegedControls: false);

        Assert.False(await _sut.Run(organization));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_NoGatewaySubscriptionId_ReturnsTrueWithoutReadingSubscription()
    {
        var organization = CreateOrganization(gatewaySubscriptionId: null);

        Assert.True(await _sut.Run(organization));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_StripeSubscriptionMissing_ThrowsConflictAndLogsIds()
    {
        var organization = CreateOrganization();
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId!, null)
            .Returns<Subscription>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = StripeConstants.ErrorCodes.ResourceMissing }
            });

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(organization));

        Assert.Equal(
            "Your organization's subscription is in an invalid state. Please contact support for assistance.",
            exception.Message);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains(organization.Id.ToString(), error);
        Assert.Contains(organization.GatewaySubscriptionId!, error);
    }

    [Fact]
    public async Task Run_StripeTransportFailure_Propagates()
    {
        var organization = CreateOrganization();
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId!, null)
            .Returns<Subscription>(_ => throw new StripeException
            {
                StripeError = new StripeError { Code = "api_error" }
            });

        await Assert.ThrowsAsync<StripeException>(() => _sut.Run(organization));
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public async Task Run_SubscriptionHasPrivilegedControlsSeatLine_ReturnsFalse()
    {
        var organization = CreateOrganization();
        ArrangeSubscription(organization,
            StripeConstants.PurchasableReferences.PasswordManagerSeat,
            StripeConstants.PurchasableReferences.PrivilegedControlsSeat);

        Assert.False(await _sut.Run(organization));
    }

    [Fact]
    public async Task Run_SubscriptionWithoutPrivilegedControlsSeatLine_ReturnsTrue()
    {
        var organization = CreateOrganization(pamSeats: 12);
        ArrangeSubscription(organization, StripeConstants.PurchasableReferences.PasswordManagerSeat);

        Assert.True(await _sut.Run(organization));
    }

    [Fact]
    public async Task Run_PreFlagOrganizationWithoutSeatsOrLine_ReturnsTrue()
    {
        var organization = CreateOrganization(pamSeats: null);
        ArrangeSubscription(organization, StripeConstants.PurchasableReferences.PasswordManagerSeat);

        Assert.True(await _sut.Run(organization));
    }

    private OrganizationEntity CreateOrganization(
        bool supportsPrivilegedControls = true,
        bool usePam = true,
        int? pamSeats = null,
        string? gatewaySubscriptionId = "sub_1")
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            UsePam = usePam,
            PamSeats = pamSeats,
            GatewayCustomerId = "cus_1",
            GatewaySubscriptionId = gatewaySubscriptionId
        };
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan(supportsPrivilegedControls));
        return organization;
    }

    private void ArrangeSubscription(OrganizationEntity organization, params string[] references) =>
        _stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId!, null)
            .Returns(new Subscription
            {
                Items = new StripeList<SubscriptionItem>
                {
                    Data = references.Select(reference => new SubscriptionItem
                    {
                        Price = new Price
                        {
                            Id = $"price_{reference}",
                            Metadata = new Dictionary<string, string>
                            {
                                [StripeConstants.MetadataKeys.PurchasableReference] = reference
                            }
                        }
                    }).ToList()
                }
            });

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan(bool supportsPrivilegedControls)
        {
            if (supportsPrivilegedControls)
            {
                PrivilegedControls = new PrivilegedControlsPlanFeatures
                {
                    StripeSeatPlanId = "privileged-controls-enterprise-seat-annually"
                };
            }
        }
    }
}
