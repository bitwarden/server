using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Premium.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class PreviewPremiumTaxCommandTests
{
    private readonly ILogger<PreviewPremiumTaxCommand> _logger = Substitute.For<ILogger<PreviewPremiumTaxCommand>>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriptionDiscountService _subscriptionDiscountService = Substitute.For<ISubscriptionDiscountService>();
    private readonly PreviewPremiumTaxCommand _command;
    private readonly User _user;

    public PreviewPremiumTaxCommandTests()
    {
        // Setup default premium plan with standard pricing
        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new PremiumPurchasable { Price = 10M, StripePriceId = Prices.PremiumAnnually },
            Storage = new PremiumPurchasable { Price = 4M, StripePriceId = Prices.StoragePlanPersonal }
        };
        _pricingClient.GetAvailablePremiumPlan().Returns(premiumPlan);

        _user = new User { Id = Guid.NewGuid(), Email = "test@example.com" };

        _command = new PreviewPremiumTaxCommand(_logger, _pricingClient, _stripeAdapter, _subscriptionDiscountService);
    }

    #region Helper Methods

    private static PremiumPurchasePreview CreatePreview(short additionalStorageGb = 0, string? coupon = null)
    {
        return new PremiumPurchasePreview
        {
            AdditionalStorageGb = additionalStorageGb,
            Coupon = coupon
        };
    }

    private static BillingAddress CreateBillingAddress(string country = "US", string postalCode = "12345")
    {
        return new BillingAddress
        {
            Country = country,
            PostalCode = postalCode
        };
    }

    #endregion

    [Fact]
    public async Task Run_PremiumWithoutStorage_ReturnsCorrectTaxAmounts()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 0,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_PremiumWithAdditionalStorage_ReturnsCorrectTaxAmounts()
    {
        var billingAddress = new BillingAddress
        {
            Country = "CA",
            PostalCode = "K1A 0A6"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 500 }],
            Total = 5500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 5,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(55.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.PremiumAnnually && item.Quantity == 1) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.StoragePlanPersonal && item.Quantity == 5)));
    }

    [Fact]
    public async Task Run_PremiumWithZeroStorage_ExcludesStorageFromItems()
    {
        var billingAddress = new BillingAddress
        {
            Country = "GB",
            PostalCode = "SW1A 1AA"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 250 }],
            Total = 2750
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 0,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(2.50m, tax);
        Assert.Equal(27.50m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_PremiumWithLargeStorage_HandlesMultipleStorageUnits()
    {
        var billingAddress = new BillingAddress
        {
            Country = "DE",
            PostalCode = "10115"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 800 }],
            Total = 8800
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 20,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(8.00m, tax);
        Assert.Equal(88.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "DE" &&
            options.CustomerDetails.Address.PostalCode == "10115" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.PremiumAnnually && item.Quantity == 1) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.StoragePlanPersonal && item.Quantity == 20)));
    }

    [Fact]
    public async Task Run_PremiumInternationalAddress_UsesCorrectAddressInfo()
    {
        var billingAddress = new BillingAddress
        {
            Country = "AU",
            PostalCode = "2000"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 450 }],
            Total = 4950
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 10,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(4.50m, tax);
        Assert.Equal(49.50m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "AU" &&
            options.CustomerDetails.Address.PostalCode == "2000" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.PremiumAnnually && item.Quantity == 1) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.StoragePlanPersonal && item.Quantity == 10)));
    }

    [Fact]
    public async Task Run_PremiumNoTax_ReturnsZeroTax()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "97330" // Example of a tax-free jurisdiction
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 3000
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 0,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(0.00m, tax);
        Assert.Equal(30.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "97330" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_NegativeStorage_TreatedAsZero()
    {
        var billingAddress = new BillingAddress
        {
            Country = "FR",
            PostalCode = "75001"
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 600 }],
            Total = 6600
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = -5,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(6.00m, tax);
        Assert.Equal(66.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "FR" &&
            options.CustomerDetails.Address.PostalCode == "75001" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_AmountConversion_CorrectlyConvertsStripeAmounts()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        // Stripe amounts are in cents
        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 123 }], // $1.23
            Total = 3123 // $31.23
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 0,
            Coupon = null
        };

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(1.23m, tax);
        Assert.Equal(31.23m, total);
    }

    [Fact]
    public async Task Run_WithValidCoupon_IncludesCouponInInvoicePreview()
    {
        var billingAddress = CreateBillingAddress();
        var preview = CreatePreview(coupon: "VALID_COUPON_CODE");

        _subscriptionDiscountService.ValidateDiscountForUserAsync(
            _user,
            "VALID_COUPON_CODE",
            DiscountAudienceType.UserHasNoPreviousSubscriptions).Returns(true);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "VALID_COUPON_CODE" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_WithCouponAndStorage_IncludesBothInInvoicePreview()
    {
        var billingAddress = CreateBillingAddress(country: "CA", postalCode: "K1A 0A6");
        var preview = CreatePreview(additionalStorageGb: 5, coupon: "STORAGE_DISCOUNT");

        _subscriptionDiscountService.ValidateDiscountForUserAsync(
            _user,
            "STORAGE_DISCOUNT",
            DiscountAudienceType.UserHasNoPreviousSubscriptions).Returns(true);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 450 }],
            Total = 4950
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(4.50m, tax);
        Assert.Equal(49.50m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "STORAGE_DISCOUNT" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.PremiumAnnually && item.Quantity == 1) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == Prices.StoragePlanPersonal && item.Quantity == 5)));
    }

    [Fact]
    public async Task Run_WithCouponWhitespace_TrimsCouponCode()
    {
        var billingAddress = CreateBillingAddress(country: "GB", postalCode: "SW1A 1AA");
        var preview = CreatePreview(coupon: "  WHITESPACE_COUPON  ");

        _subscriptionDiscountService.ValidateDiscountForUserAsync(
            _user,
            "WHITESPACE_COUPON",
            DiscountAudienceType.UserHasNoPreviousSubscriptions).Returns(true);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 250 }],
            Total = 2750
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(2.50m, tax);
        Assert.Equal(27.50m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "WHITESPACE_COUPON" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_WithNullCoupon_ExcludesCouponFromInvoicePreview()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 0,
            Coupon = null
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.Discounts == null &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_WithEmptyCoupon_ExcludesCouponFromInvoicePreview()
    {
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var preview = new PremiumPurchasePreview
        {
            AdditionalStorageGb = 0,
            Coupon = ""
        };

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.Discounts == null &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == Prices.PremiumAnnually &&
            options.SubscriptionDetails.Items[0].Quantity == 1));
    }

    [Fact]
    public async Task Run_WithValidCoupon_ValidatesCouponAndAppliesDiscount()
    {
        var billingAddress = CreateBillingAddress();
        var preview = CreatePreview(coupon: "VALID_DISCOUNT");

        _subscriptionDiscountService.ValidateDiscountForUserAsync(
            _user,
            "VALID_DISCOUNT",
            DiscountAudienceType.UserHasNoPreviousSubscriptions).Returns(true);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 200 }],
            Total = 2200
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(2.00m, tax);
        Assert.Equal(22.00m, total);

        await _subscriptionDiscountService.Received(1).ValidateDiscountForUserAsync(
            _user,
            "VALID_DISCOUNT",
            DiscountAudienceType.UserHasNoPreviousSubscriptions);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "VALID_DISCOUNT"));
    }

    [Fact]
    public async Task Run_WithInvalidCoupon_IgnoresCouponAndProceeds()
    {
        var billingAddress = CreateBillingAddress();
        var preview = CreatePreview(coupon: "INVALID_COUPON");

        _subscriptionDiscountService.ValidateDiscountForUserAsync(
            _user,
            "INVALID_COUPON",
            DiscountAudienceType.UserHasNoPreviousSubscriptions).Returns(false);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _subscriptionDiscountService.Received(1).ValidateDiscountForUserAsync(
            _user,
            "INVALID_COUPON",
            DiscountAudienceType.UserHasNoPreviousSubscriptions);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts == null || options.Discounts.Count == 0));
    }

    [Fact]
    public async Task Run_WithCouponForUserWithPreviousSubscription_IgnoresCouponAndProceeds()
    {
        var billingAddress = CreateBillingAddress();
        var preview = CreatePreview(coupon: "NEW_USER_ONLY");

        // User has previous subscription, so validation fails
        _subscriptionDiscountService.ValidateDiscountForUserAsync(
            _user,
            "NEW_USER_ONLY",
            DiscountAudienceType.UserHasNoPreviousSubscriptions).Returns(false);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, preview, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _subscriptionDiscountService.Received(1).ValidateDiscountForUserAsync(
            _user,
            "NEW_USER_ONLY",
            DiscountAudienceType.UserHasNoPreviousSubscriptions);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts == null || options.Discounts.Count == 0));
    }
}
