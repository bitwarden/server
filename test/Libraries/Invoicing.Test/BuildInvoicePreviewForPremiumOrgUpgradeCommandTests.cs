using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Commands;
using Bit.Invoicing.InvoicePreviews.Models;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Invoicing.Test;

public class BuildInvoicePreviewForPremiumOrgUpgradeCommandTests
{
    private const string PremiumSeatPriceId = "premium-annually";
    private const string PremiumStoragePriceId = "storage-gb-annually";
    private const string SubscriptionId = "sub_premium";
    private const string CustomerId = "cus_premium";

    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly BuildInvoicePreviewForPremiumOrgUpgradeCommand _sut;

    public BuildInvoicePreviewForPremiumOrgUpgradeCommandTests() =>
        _sut = new BuildInvoicePreviewForPremiumOrgUpgradeCommand(
            _pricingClient, _stripeAdapter, _invoicePreviewService);

    private static readonly BillingAddress Address = new() { Country = "US", PostalCode = "12345" };

    [Fact]
    public async Task Run_WhenUserIsNotPremium_ThrowsBadRequest()
    {
        var user = new User { Id = Guid.NewGuid(), Premium = false, GatewaySubscriptionId = SubscriptionId };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Run(user, PlanType.FamiliesAnnually, Address));

        Assert.Equal("User does not have an active Premium subscription.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Run_WhenGatewaySubscriptionIdMissing_ThrowsBadRequest(string? gatewaySubscriptionId)
    {
        var user = new User { Id = Guid.NewGuid(), Premium = true, GatewaySubscriptionId = gatewaySubscriptionId };

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(user, PlanType.FamiliesAnnually, Address));
    }

    [Fact]
    public async Task Run_WhenGatewayCustomerIdMissing_ThrowsBadRequest()
    {
        var user = new User { Id = Guid.NewGuid(), Premium = true, GatewaySubscriptionId = SubscriptionId };

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(user, PlanType.FamiliesAnnually, Address));
    }

    [Theory]
    [InlineData(PlanType.TeamsMonthly)]
    [InlineData(PlanType.FamiliesAnnually2019)]
    [InlineData(PlanType.Free)]
    public async Task Run_WhenTargetPlanIsNotAnAnnualOrganizationPlan_ThrowsBadRequest(PlanType planType)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Run(PremiumUser(), planType, Address));

        Assert.Contains("Cannot upgrade Premium subscription", exception.Message);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_WhenStripeRejectsTheTaxLocation_ThrowsBadRequest()
    {
        var user = PremiumUser();
        ArrangeFamiliesUpgrade();
        ArrangePreviewFailure("tax", "customer_tax_location_invalid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Run(user, PlanType.FamiliesAnnually, Address));

        Assert.Contains("Your location wasn't recognized", exception.Message);
    }

    [Fact]
    public async Task Run_WhenStripeFailsForAnotherReason_PropagatesTheStripeException()
    {
        var user = PremiumUser();
        ArrangeFamiliesUpgrade();
        ArrangePreviewFailure("boom", "resource_missing");

        await Assert.ThrowsAsync<StripeException>(() => _sut.Run(user, PlanType.FamiliesAnnually, Address));
    }

    [Fact]
    public async Task Run_WhenNoPasswordManagerItemOnSubscription_ThrowsBadRequest()
    {
        var user = PremiumUser();
        ArrangeSubscription(SubscriptionWithItems(("si_other", "some-other-price")));
        ArrangePremiumPlans();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Run(user, PlanType.FamiliesAnnually, Address));

        Assert.Equal("Premium subscription password manager item not found.", exception.Message);
    }

    [Fact]
    public async Task Run_BuildsPreviewOptionsForTheTargetPlan()
    {
        var user = PremiumUser();
        ArrangeFamiliesUpgrade();
        ArrangePreview();

        await _sut.Run(user, PlanType.FamiliesAnnually, Address);

        var options = CapturedOptions();
        Assert.Equal(SubscriptionId, options.Subscription);
        Assert.Equal(CustomerId, options.Customer);
        Assert.True(options.AutomaticTax.Enabled);
        Assert.Equal("US", options.CustomerDetails.Address.Country);
        Assert.Equal("12345", options.CustomerDetails.Address.PostalCode);
        Assert.Equal("always_invoice", options.SubscriptionDetails.ProrationBehavior);

        var item = Assert.Single(options.SubscriptionDetails.Items);
        Assert.Equal("si_pm", item.Id);
        Assert.Equal("2020-families-org-annually", item.Price);
        Assert.Equal(1, item.Quantity);
        Assert.Null(item.Deleted);
    }

    [Fact]
    public async Task Run_WhenSubscriptionHasStorage_DeletesTheStorageItem()
    {
        var user = PremiumUser();
        ArrangeSubscription(SubscriptionWithItems(
            ("si_pm", PremiumSeatPriceId), ("si_storage", PremiumStoragePriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(PlanType.TeamsAnnually, ProductTierType.Teams, seatPlanId: "2023-teams-org-seat-annually");
        ArrangePreview();

        await _sut.Run(user, PlanType.TeamsAnnually, Address);

        var items = CapturedOptions().SubscriptionDetails.Items;
        Assert.Equal(2, items.Count);
        var deleted = Assert.Single(items, i => i.Deleted == true);
        Assert.Equal("si_storage", deleted.Id);
        Assert.Single(items, i => i.Id == "si_pm" && i.Quantity == 1);
    }

    [Fact]
    public async Task Run_WhenSubscriptionHasNoStorage_IncludesOnlyTheSeatSwap()
    {
        var user = PremiumUser();
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(PlanType.TeamsAnnually, ProductTierType.Teams, seatPlanId: "2023-teams-org-seat-annually");
        ArrangePreview();

        await _sut.Run(user, PlanType.TeamsAnnually, Address);

        var items = CapturedOptions().SubscriptionDetails.Items;
        Assert.Single(items);
        Assert.DoesNotContain(items, i => i.Deleted == true);
    }

    [Fact]
    public async Task Run_WhenTargetPlanIsNotSeatBased_UsesTheFlatPlanPrice()
    {
        var user = PremiumUser();
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(PlanType.FamiliesAnnually, ProductTierType.Families,
            planId: "2020-families-org-annually", seatPlanId: null);
        ArrangePreview();

        await _sut.Run(user, PlanType.FamiliesAnnually, Address);

        var item = Assert.Single(CapturedOptions().SubscriptionDetails.Items);
        Assert.Equal("2020-families-org-annually", item.Price);
    }

    [Theory]
    [InlineData(PlanType.FamiliesAnnually, ProductTierType.Families, PlanTierType.Families)]
    [InlineData(PlanType.TeamsAnnually, ProductTierType.Teams, PlanTierType.Teams)]
    [InlineData(PlanType.EnterpriseAnnually, ProductTierType.Enterprise, PlanTierType.Enterprise)]
    public async Task Run_ProjectsTheTargetTierAlwaysAnnually(
        PlanType planType, ProductTierType productTier, PlanTierType expectedTier)
    {
        var user = PremiumUser();
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(planType, productTier, seatPlanId: "target-seat-price");
        var preview = ArrangePreview();

        var result = await _sut.Run(user, planType, Address);

        Assert.Same(preview, result);
        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), expectedTier, PlanCadenceType.Annually);
    }

    private static User PremiumUser() => new()
    {
        Id = Guid.NewGuid(),
        Premium = true,
        GatewaySubscriptionId = SubscriptionId,
        GatewayCustomerId = CustomerId
    };

    private void ArrangeSubscription(Subscription subscription) =>
        _stripeAdapter.GetSubscriptionAsync(SubscriptionId, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

    private void ArrangePremiumPlans() =>
        _pricingClient.ListPremiumPlans().Returns([
            new PremiumPlan
            {
                Name = "premium-annually",
                Available = true,
                Seat = new PremiumPurchasable { StripePriceId = PremiumSeatPriceId, Price = 10m, Provided = 1 },
                Storage = new PremiumPurchasable { StripePriceId = PremiumStoragePriceId, Price = 4m, Provided = 1 }
            }
        ]);

    private void ArrangeTargetPlan(
        PlanType planType, ProductTierType productTier, string? planId = null, string? seatPlanId = null) =>
        _pricingClient.GetPlanOrThrow(planType).Returns(new TestPlan(productTier, planId, seatPlanId));

    /// <summary>Seat-only Premium upgrading to annual Families; shared by every test that reaches the preview call.</summary>
    private void ArrangeFamiliesUpgrade()
    {
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(
            PlanType.FamiliesAnnually, ProductTierType.Families, seatPlanId: "2020-families-org-annually");
    }

    private void ArrangePreviewFailure(string message, string stripeErrorCode) =>
        _invoicePreviewService.GetInvoicePreviewAsync(
                Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(Task.FromException<InvoicePreview>(new StripeException(message)
            {
                StripeError = new StripeError { Code = stripeErrorCode }
            }));

    private InvoicePreview ArrangePreview()
    {
        var preview = SamplePreview();
        _invoicePreviewService.GetInvoicePreviewAsync(
                Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(preview);
        return preview;
    }

    private InvoiceCreatePreviewOptions CapturedOptions() =>
        (InvoiceCreatePreviewOptions)_invoicePreviewService.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IInvoicePreviewService.GetInvoicePreviewAsync))
            .GetArguments()[0]!;

    private static Subscription SubscriptionWithItems(params (string ItemId, string PriceId)[] items) =>
        new()
        {
            Id = SubscriptionId,
            Items = new StripeList<SubscriptionItem>
            {
                Data = items
                    .Select(i => new SubscriptionItem { Id = i.ItemId, Price = new Price { Id = i.PriceId } })
                    .ToList()
            }
        };

    private static InvoicePreview SamplePreview() => new()
    {
        PlanTier = PlanTierType.Families,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 26.67m }
        },
        EstimatedTax = 2m,
        Total = 22m,
        AmountDue = 22m
    };

    private sealed record TestPlan : Bit.Core.Models.StaticStore.Plan
    {
        public TestPlan(ProductTierType productTier, string? planId, string? seatPlanId)
        {
            ProductTier = productTier;
            IsAnnual = true;
            PasswordManager = new PasswordManagerPlanFeatures
            {
                StripePlanId = planId,
                StripeSeatPlanId = seatPlanId
            };
        }
    }
}
