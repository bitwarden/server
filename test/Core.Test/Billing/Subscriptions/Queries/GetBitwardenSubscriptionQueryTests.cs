using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Subscriptions.Queries;

using static StripeConstants;

public class GetBitwardenSubscriptionQueryTests
{
    private readonly ILogger<GetBitwardenSubscriptionQuery> _logger = Substitute.For<ILogger<GetBitwardenSubscriptionQuery>>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly GetBitwardenSubscriptionQuery _query;

    public GetBitwardenSubscriptionQueryTests()
    {
        _query = new GetBitwardenSubscriptionQuery(
            _logger,
            _pricingClient,
            _stripeAdapter);
    }

    [Fact]
    public async Task Run_UserWithoutGatewaySubscriptionId_ReturnsNull()
    {
        var user = CreateUser();
        user.GatewaySubscriptionId = null;

        var result = await _query.Run(user);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_UserWithEmptyGatewaySubscriptionId_ReturnsNull()
    {
        var user = CreateUser();
        user.GatewaySubscriptionId = string.Empty;

        var result = await _query.Run(user);

        Assert.Null(result);
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_StripeSubscriptionNotFound_ReturnsNull()
    {
        var user = CreateUser();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = ErrorCodes.ResourceMissing } });

        var result = await _query.Run(user);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_StripeExceptionNotResourceMissing_Throws()
    {
        var user = CreateUser();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "api_error" } });

        await Assert.ThrowsAsync<StripeException>(() => _query.Run(user));
    }

    [Fact]
    public async Task Run_IncompleteStatus_ReturnsBitwardenSubscriptionWithSuspension()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Incomplete);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.Incomplete, result.Status);
        Assert.NotNull(result.Suspension);
        Assert.Equal(subscription.Created.AddHours(23), result.Suspension);
        Assert.Equal(1, result.GracePeriod);
        Assert.Null(result.NextCharge);
        Assert.Null(result.CancelAt);
    }

    [Fact]
    public async Task Run_IncompleteExpiredStatus_ReturnsBitwardenSubscriptionWithSuspension()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.IncompleteExpired);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.IncompleteExpired, result.Status);
        Assert.NotNull(result.Suspension);
        Assert.Equal(subscription.Created.AddHours(23), result.Suspension);
        Assert.Equal(1, result.GracePeriod);
    }

    [Fact]
    public async Task Run_TrialingStatus_ReturnsBitwardenSubscriptionWithNextCharge()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Trialing);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.Trialing, result.Status);
        Assert.NotNull(result.NextCharge);
        Assert.Equal(subscription.Items.First().CurrentPeriodEnd, result.NextCharge);
        Assert.Null(result.Suspension);
        Assert.Null(result.GracePeriod);
    }

    [Fact]
    public async Task Run_ActiveStatus_ReturnsBitwardenSubscriptionWithNextCharge()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.Active, result.Status);
        Assert.NotNull(result.NextCharge);
        Assert.Equal(subscription.Items.First().CurrentPeriodEnd, result.NextCharge);
        Assert.Null(result.Suspension);
        Assert.Null(result.GracePeriod);
    }

    [Fact]
    public async Task Run_ActiveStatusWithCancelAt_ReturnsCancelAt()
    {
        var user = CreateUser();
        var cancelAt = DateTime.UtcNow.AddMonths(1);
        var subscription = CreateSubscription(SubscriptionStatus.Active, cancelAt: cancelAt);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.Active, result.Status);
        Assert.Equal(cancelAt, result.CancelAt);
    }

    [Fact]
    public async Task Run_PastDueStatus_WithOpenInvoices_ReturnsSuspension()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.PastDue, collectionMethod: "charge_automatically");
        var premiumPlans = CreatePremiumPlans();
        var openInvoice = CreateInvoice();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());
        _stripeAdapter.SearchInvoiceAsync(Arg.Any<InvoiceSearchOptions>())
            .Returns([openInvoice]);

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.PastDue, result.Status);
        Assert.NotNull(result.Suspension);
        Assert.Equal(openInvoice.Created.AddDays(14), result.Suspension);
        Assert.Equal(14, result.GracePeriod);
    }

    [Fact]
    public async Task Run_PastDueStatus_WithoutOpenInvoices_ReturnsNoSuspension()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.PastDue);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());
        _stripeAdapter.SearchInvoiceAsync(Arg.Any<InvoiceSearchOptions>())
            .Returns([]);

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.PastDue, result.Status);
        Assert.Null(result.Suspension);
        Assert.Null(result.GracePeriod);
    }

    [Fact]
    public async Task Run_UnpaidStatus_WithOpenInvoices_ReturnsSuspension()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Unpaid, collectionMethod: "charge_automatically");
        var premiumPlans = CreatePremiumPlans();
        var openInvoice = CreateInvoice();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());
        _stripeAdapter.SearchInvoiceAsync(Arg.Any<InvoiceSearchOptions>())
            .Returns([openInvoice]);

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.Unpaid, result.Status);
        Assert.NotNull(result.Suspension);
        Assert.Equal(14, result.GracePeriod);
    }

    [Fact]
    public async Task Run_CanceledStatus_ReturnsCanceledDate()
    {
        var user = CreateUser();
        var canceledAt = DateTime.UtcNow.AddDays(-5);
        var subscription = CreateSubscription(SubscriptionStatus.Canceled, canceledAt: canceledAt);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.Canceled, result.Status);
        Assert.Equal(canceledAt, result.Canceled);
        Assert.Null(result.Suspension);
        Assert.Null(result.NextCharge);
    }

    [Fact]
    public async Task Run_UnmanagedStatus_ThrowsConflictException()
    {
        var user = CreateUser();
        var subscription = CreateSubscription("unmanaged_status");
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        await Assert.ThrowsAsync<ConflictException>(() => _query.Run(user));
    }

    [Fact]
    public async Task Run_WithAdditionalStorage_IncludesStorageInCart()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active, includeStorage: true);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.NotNull(result.Cart.PasswordManager.AdditionalStorage);
        Assert.Equal("additionalStorageGB", result.Cart.PasswordManager.AdditionalStorage.TranslationKey);
        Assert.Equal(2, result.Cart.PasswordManager.AdditionalStorage.Quantity);
        Assert.NotNull(result.Storage);
    }

    [Fact]
    public async Task Run_WithoutAdditionalStorage_ExcludesStorageFromCart()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active, includeStorage: false);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Null(result.Cart.PasswordManager.AdditionalStorage);
        Assert.NotNull(result.Storage);
    }

    [Fact]
    public async Task Run_WithCartLevelDiscount_IncludesDiscountInCart()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        subscription.Customer.Discount = CreateDiscount(discountType: "cart");
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.NotNull(result.Cart.Discount);
        Assert.Equal(BitwardenDiscountType.PercentOff, result.Cart.Discount.Type);
        Assert.Equal(20, result.Cart.Discount.Value);
    }

    [Fact]
    public async Task Run_WithProductLevelDiscount_IncludesDiscountInCartItem()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var productDiscount = CreateDiscount(discountType: "product", productId: "prod_premium_seat");
        subscription.Discounts = [productDiscount];
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.NotNull(result.Cart.PasswordManager.Seats.Discount);
        Assert.Equal(BitwardenDiscountType.PercentOff, result.Cart.PasswordManager.Seats.Discount.Type);
    }

    [Fact]
    public async Task Run_WithoutMaxStorageGb_ReturnsNullStorage()
    {
        var user = CreateUser();
        user.MaxStorageGb = null;
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Null(result.Storage);
    }

    [Fact]
    public async Task Run_CalculatesStorageCorrectly()
    {
        var user = CreateUser();
        user.Storage = 5368709120; // 5 GB in bytes
        user.MaxStorageGb = 10;
        var subscription = CreateSubscription(SubscriptionStatus.Active, includeStorage: true);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.NotNull(result.Storage);
        Assert.Equal(10, result.Storage.Available);
        Assert.Equal(5.0, result.Storage.Used);
        Assert.NotEmpty(result.Storage.ReadableUsed);
    }

    [Fact]
    public async Task Run_TaxEstimation_WithInvoiceUpcomingNoneError_ReturnsZeroTax()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = ErrorCodes.InvoiceUpcomingNone } });

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(0, result.Cart.EstimatedTax);
    }

    [Fact]
    public async Task Run_MissingPasswordManagerSeatsItem_ThrowsConflictException()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        subscription.Items = new StripeList<SubscriptionItem>
        {
            Data = []
        };
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);

        await Assert.ThrowsAsync<ConflictException>(() => _query.Run(user));
    }

    [Fact]
    public async Task Run_IncludesEstimatedTax()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var premiumPlans = CreatePremiumPlans();
        var invoice = CreateInvoicePreview(totalTax: 500); // $5.00 tax

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(5.0m, result.Cart.EstimatedTax);
    }

    [Fact]
    public async Task Run_SetsCadenceToAnnually()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(PlanCadenceType.Annually, result.Cart.Cadence);
    }

    [Fact]
    public async Task Run_UserOnLegacyPricing_ReturnsCostFromPricingService()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active, legacyPricing: true);
        var premiumPlans = CreatePremiumPlans();
        var availablePlan = premiumPlans.First(p => p.Available);

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);

        var previewInvoice = CreateInvoicePreview(totalTax: 150);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(previewInvoice);

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(availablePlan.Seat.Price, result.Cart.PasswordManager.Seats.Cost);
        Assert.Equal(1.50m, result.Cart.EstimatedTax);
    }

    [Fact]
    public async Task Run_UserOnLegacyPricing_CallsPreviewInvoiceWithRebuiltSubscription()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active, legacyPricing: true);
        var premiumPlans = CreatePremiumPlans();
        var availablePlan = premiumPlans.First(p => p.Available);

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);

        var previewInvoice = CreateInvoicePreview();
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(previewInvoice);

        await _query.Run(user);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(opts =>
                opts.Subscription == null &&
                opts.AutomaticTax != null &&
                opts.AutomaticTax.Enabled == true &&
                opts.SubscriptionDetails != null &&
                opts.SubscriptionDetails.Items.Any(i =>
                    i.Price == availablePlan.Seat.StripePriceId &&
                    i.Quantity == 1)));
    }

    [Fact]
    public async Task Run_UserOnCurrentPricing_ReturnsCostFromSubscriptionItem()
    {
        var user = CreateUser();
        var subscription = CreateSubscription(SubscriptionStatus.Active, legacyPricing: false);
        var premiumPlans = CreatePremiumPlans();

        _stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(CreateInvoicePreview());

        var result = await _query.Run(user);

        Assert.NotNull(result);
        Assert.Equal(19.80m, result.Cart.PasswordManager.Seats.Cost);
    }

    #region Helper Methods

    private static User CreateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            GatewaySubscriptionId = "sub_test123",
            MaxStorageGb = 1,
            Storage = 1073741824 // 1 GB in bytes
        };
    }

    private static Subscription CreateSubscription(
        string status,
        bool includeStorage = false,
        bool legacyPricing = false,
        DateTime? cancelAt = null,
        DateTime? canceledAt = null,
        string collectionMethod = "charge_automatically")
    {
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var seatPriceId = legacyPricing ? "price_legacy_premium_seat" : "price_premium_seat";
        var seatUnitAmount = legacyPricing ? 1000 : 1980;
        var items = new List<SubscriptionItem>
        {
            new()
            {
                Id = "si_premium_seat",
                Price = new Price
                {
                    Id = seatPriceId,
                    UnitAmountDecimal = seatUnitAmount,
                    Product = new Product { Id = "prod_premium_seat" }
                },
                Quantity = 1,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = currentPeriodEnd
            }
        };

        if (includeStorage)
        {
            items.Add(new SubscriptionItem
            {
                Id = "si_storage",
                Price = new Price
                {
                    Id = "price_storage",
                    UnitAmountDecimal = 400,
                    Product = new Product { Id = "prod_storage" }
                },
                Quantity = 2,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = currentPeriodEnd
            });
        }

        return new Subscription
        {
            Id = "sub_test123",
            Status = status,
            Created = DateTime.UtcNow.AddMonths(-1),
            AutomaticTax = new SubscriptionAutomaticTax { Enabled = true },
            Customer = new Customer
            {
                Id = "cus_test123",
                Discount = null
            },
            Items = new StripeList<SubscriptionItem>
            {
                Data = items
            },
            CancelAt = cancelAt,
            CanceledAt = canceledAt,
            CollectionMethod = collectionMethod,
            Discounts = []
        };
    }

    private static List<Bit.Core.Billing.Pricing.Premium.Plan> CreatePremiumPlans()
    {
        return
        [
            new()
            {
                Name = "Premium",
                Available = true,
                Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
                {
                    StripePriceId = "price_premium_seat",
                    Price = 19.80m,
                    Provided = 1
                },
                Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
                {
                    StripePriceId = "price_storage",
                    Price = 4.0m,
                    Provided = 1
                }
            },
            new()
            {
                Name = "Premium",
                Available = false,
                LegacyYear = 2024,
                Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
                {
                    StripePriceId = "price_legacy_premium_seat",
                    Price = 10.0m,
                    Provided = 1
                },
                Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
                {
                    StripePriceId = "price_storage",
                    Price = 4.0m,
                    Provided = 1
                }
            }
        ];
    }

    private static Invoice CreateInvoice()
    {
        return new Invoice
        {
            Id = "in_test123",
            Created = DateTime.UtcNow.AddDays(-10),
            PeriodEnd = DateTime.UtcNow.AddDays(-5),
            Attempted = true,
            Status = "open"
        };
    }

    private static Invoice CreateInvoicePreview(long totalTax = 0)
    {
        var taxes = totalTax > 0
            ? new List<InvoiceTotalTax> { new() { Amount = totalTax } }
            : new List<InvoiceTotalTax>();

        return new Invoice
        {
            Id = "in_preview",
            TotalTaxes = taxes
        };
    }

    private static Discount CreateDiscount(string discountType = "cart", string? productId = null)
    {
        var coupon = new Coupon
        {
            Valid = true,
            PercentOff = 20,
            AppliesTo = discountType == "product" && productId != null
                ? new CouponAppliesTo { Products = [productId] }
                : new CouponAppliesTo { Products = [] }
        };

        return new Discount
        {
            Coupon = coupon
        };
    }

    #endregion
}
