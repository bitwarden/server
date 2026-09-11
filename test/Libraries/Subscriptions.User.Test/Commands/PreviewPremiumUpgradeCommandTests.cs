using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Commands;
using Bit.Subscriptions.User.Models.Requests;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test.Commands;

public class PreviewPremiumUpgradeCommandTests
{
    private const string PremiumSeatPriceId = "premium-annually";
    private const string PremiumStoragePriceId = "storage-gb-annually";
    private const string SubscriptionId = "sub_premium";
    private const string CustomerId = "cus_premium";

    private readonly RecordingLogger<PreviewPremiumUpgradeCommand> _logger = new();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly PreviewPremiumUpgradeCommand _sut;

    public PreviewPremiumUpgradeCommandTests() =>
        _sut = new PreviewPremiumUpgradeCommand(_logger, _pricingClient, _stripeAdapter, _invoicePreviewService);

    [Theory]
    [InlineData(ProductTierType.Free)]
    [InlineData(ProductTierType.TeamsStarter)]
    [InlineData((ProductTierType)99)]
    public async Task Run_WhenTargetTierIsNotUpgradable_ThrowsBadRequestWithModelState(ProductTierType targetProductTierType)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Run(PremiumUser(), Request(targetProductTierType)));

        Assert.True(exception.ModelState!.ContainsKey(nameof(PreviewPremiumUpgradeRequest.TargetProductTierType)));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_WhenBillingAddressIsNull_ThrowsBadRequestWithModelState()
    {
        var request = new PreviewPremiumUpgradeRequest { TargetProductTierType = ProductTierType.Teams, BillingAddress = null! };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(PremiumUser(), request));

        Assert.True(exception.ModelState!.ContainsKey(nameof(PreviewPremiumUpgradeRequest.BillingAddress)));
    }

    [Theory]
    [InlineData(null, "12345", nameof(BillingAddressRequest.Country))]
    [InlineData("", "12345", nameof(BillingAddressRequest.Country))]
    [InlineData("  ", "12345", nameof(BillingAddressRequest.Country))]
    [InlineData("USA", "12345", nameof(BillingAddressRequest.Country))]
    [InlineData("US", null, nameof(BillingAddressRequest.PostalCode))]
    [InlineData("US", "", nameof(BillingAddressRequest.PostalCode))]
    [InlineData("US", "   ", nameof(BillingAddressRequest.PostalCode))]
    public async Task Run_WhenBillingAddressIsMalformed_ThrowsBadRequestWithModelState(
        string? country, string? postalCode, string expectedKey)
    {
        var request = new PreviewPremiumUpgradeRequest
        {
            TargetProductTierType = ProductTierType.Teams,
            BillingAddress = new BillingAddressRequest { Country = country!, PostalCode = postalCode! }
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(PremiumUser(), request));

        Assert.True(exception.ModelState!.ContainsKey(expectedKey));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_WhenUserIsNotPremium_ThrowsBadRequest()
    {
        var user = new UserEntity { Id = Guid.NewGuid(), Premium = false, GatewaySubscriptionId = SubscriptionId, GatewayCustomerId = CustomerId };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(user, Request(ProductTierType.Families)));

        Assert.Equal("User does not have an active Premium subscription.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Run_WhenGatewaySubscriptionIdMissing_ThrowsConflictAndLogs(string? gatewaySubscriptionId)
    {
        var user = new UserEntity { Id = Guid.NewGuid(), Premium = true, GatewaySubscriptionId = gatewaySubscriptionId };

        await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(user, Request(ProductTierType.Families)));

        Assert.Single(_logger.Errors);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default!, default);
    }

    [Fact]
    public async Task Run_WhenNoPasswordManagerItemOnSubscription_ThrowsConflictAndLogs()
    {
        ArrangeSubscription(SubscriptionWithItems(("si_other", "some-other-price")));
        ArrangePremiumPlans();

        await Assert.ThrowsAsync<ConflictException>(() => _sut.Run(PremiumUser(), Request(ProductTierType.Families)));

        Assert.Single(_logger.Errors);
        await _invoicePreviewService.DidNotReceive().GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>());
    }

    [Fact]
    public async Task Run_WhenGatewayCustomerIdMissing_StillPreviewsAgainstTheSubscription()
    {
        var user = PremiumUser();
        user.GatewayCustomerId = null;
        ArrangeFamiliesUpgrade();
        ArrangePreview();

        await _sut.Run(user, Request(ProductTierType.Families));

        var options = CapturedOptions();
        Assert.Null(options.Customer);
        Assert.Equal(SubscriptionId, options.Subscription);
    }

    [Fact]
    public async Task Run_WhenStripeRejectsTheTaxLocation_ThrowsBadRequest()
    {
        ArrangeFamiliesUpgrade();
        ArrangePreviewFailure("customer_tax_location_invalid");

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Run(PremiumUser(), Request(ProductTierType.Families)));

        Assert.Contains("Your location wasn't recognized", exception.Message);
    }

    [Fact]
    public async Task Run_WhenStripeFailsForAnotherReason_PropagatesTheStripeException()
    {
        ArrangeFamiliesUpgrade();
        ArrangePreviewFailure("resource_missing");

        await Assert.ThrowsAsync<StripeException>(() => _sut.Run(PremiumUser(), Request(ProductTierType.Families)));
    }

    [Fact]
    public async Task Run_BuildsPreviewOptionsForTheTargetPlan()
    {
        ArrangeFamiliesUpgrade();
        ArrangePreview();

        await _sut.Run(PremiumUser(), Request(ProductTierType.Families));

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
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId), ("si_storage", PremiumStoragePriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(PlanType.TeamsAnnually, ProductTierType.Teams, seatPlanId: "2023-teams-org-seat-annually");
        ArrangePreview();

        await _sut.Run(PremiumUser(), Request(ProductTierType.Teams));

        var items = CapturedOptions().SubscriptionDetails.Items;
        Assert.Equal(2, items.Count);
        var deleted = Assert.Single(items, i => i.Deleted is true);
        Assert.Equal("si_storage", deleted.Id);
        Assert.Single(items, i => i.Id == "si_pm" && i.Price == "2023-teams-org-seat-annually" && i.Quantity == 1);
    }

    [Fact]
    public async Task Run_WhenSubscriptionHasNoStorage_IncludesOnlyTheSeatSwap()
    {
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(PlanType.TeamsAnnually, ProductTierType.Teams, seatPlanId: "2023-teams-org-seat-annually");
        ArrangePreview();

        await _sut.Run(PremiumUser(), Request(ProductTierType.Teams));

        var items = CapturedOptions().SubscriptionDetails.Items;
        Assert.Single(items);
        Assert.DoesNotContain(items, i => i.Deleted is true);
    }

    [Theory]
    [InlineData(ProductTierType.Families, PlanType.FamiliesAnnually, PlanTierType.Families)]
    [InlineData(ProductTierType.Teams, PlanType.TeamsAnnually, PlanTierType.Teams)]
    [InlineData(ProductTierType.Enterprise, PlanType.EnterpriseAnnually, PlanTierType.Enterprise)]
    public async Task Run_ProjectsTheTargetTierAlwaysAnnually(
        ProductTierType targetProductTierType, PlanType expectedPlanType, PlanTierType expectedTier)
    {
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(expectedPlanType, targetProductTierType, seatPlanId: "target-seat-price");
        var preview = ArrangePreview();

        var result = await _sut.Run(PremiumUser(), Request(targetProductTierType));

        Assert.Same(preview, result);
        await _pricingClient.Received(1).GetPlanOrThrow(expectedPlanType);
        await _invoicePreviewService.Received(1).GetInvoicePreviewAsync(
            Arg.Any<InvoiceCreatePreviewOptions>(), expectedTier, PlanCadenceType.Annually);
    }

    private static UserEntity PremiumUser() => new()
    {
        Id = Guid.NewGuid(),
        Premium = true,
        GatewaySubscriptionId = SubscriptionId,
        GatewayCustomerId = CustomerId
    };

    private static PreviewPremiumUpgradeRequest Request(ProductTierType targetProductTierType) => new()
    {
        TargetProductTierType = targetProductTierType,
        BillingAddress = new BillingAddressRequest { Country = "US", PostalCode = "12345" }
    };

    private void ArrangeFamiliesUpgrade()
    {
        ArrangeSubscription(SubscriptionWithItems(("si_pm", PremiumSeatPriceId)));
        ArrangePremiumPlans();
        ArrangeTargetPlan(PlanType.FamiliesAnnually, ProductTierType.Families, planId: "2020-families-org-annually", seatPlanId: null);
    }

    private void ArrangeSubscription(Subscription subscription) =>
        _stripeAdapter.GetSubscriptionAsync(SubscriptionId, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

    private void ArrangePremiumPlans() =>
        _pricingClient.ListPremiumPlans().Returns(
        [
            new PremiumPlan
            {
                Seat = new PremiumPurchasable { StripePriceId = PremiumSeatPriceId },
                Storage = new PremiumPurchasable { StripePriceId = PremiumStoragePriceId }
            }
        ]);

    private void ArrangeTargetPlan(PlanType planType, ProductTierType productTier, string? planId = null, string? seatPlanId = null) =>
        _pricingClient.GetPlanOrThrow(planType).Returns(new TestPlan(productTier, planId, seatPlanId));

    private InvoicePreview ArrangePreview()
    {
        var preview = new InvoicePreview
        {
            PlanTier = PlanTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new PasswordManagerInvoiceItems
            {
                Prorations = [new PurchasableProration { Charge = 26.67m, Credit = 6.67m, Tax = 2m, Total = 20m, Months = 8 }]
            },
            EstimatedTax = 2m,
            Total = 22m,
            AmountDue = 22m
        };
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(preview);
        return preview;
    }

    private void ArrangePreviewFailure(string code) =>
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns<InvoicePreview>(_ => throw new StripeException { StripeError = new StripeError { Code = code } });

    private InvoiceCreatePreviewOptions CapturedOptions() =>
        (InvoiceCreatePreviewOptions)_invoicePreviewService.ReceivedCalls().Single().GetArguments()[0]!;

    private static Subscription SubscriptionWithItems(params (string Id, string PriceId)[] items) => new()
    {
        Id = SubscriptionId,
        Items = new StripeList<SubscriptionItem>
        {
            Data = items.Select(item => new SubscriptionItem { Id = item.Id, Price = new Price { Id = item.PriceId } }).ToList()
        }
    };

    private sealed record TestPlan : Bit.Core.Models.StaticStore.Plan
    {
        public TestPlan(ProductTierType productTier, string? planId, string? seatPlanId)
        {
            ProductTier = productTier;
            IsAnnual = true;
            PasswordManager = new PasswordManagerPlanFeatures { StripePlanId = planId!, StripeSeatPlanId = seatPlanId! };
        }
    }
}
