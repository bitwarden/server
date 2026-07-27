using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Test.Billing.Mocks.Plans;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Organizations.Commands;

public class PreviewOrganizationTaxCommandTests
{
    private readonly ILogger<PreviewOrganizationTaxCommand> _logger = Substitute.For<ILogger<PreviewOrganizationTaxCommand>>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriptionDiscountService _subscriptionDiscountService = Substitute.For<ISubscriptionDiscountService>();
    private readonly PreviewOrganizationTaxCommand _command;
    private readonly User _user;

    public PreviewOrganizationTaxCommandTests()
    {
        _user = new User { Id = Guid.NewGuid(), Email = "test@example.com" };
        _command = new PreviewOrganizationTaxCommand(_logger, _pricingClient, _stripeAdapter, _subscriptionDiscountService);
    }

    #region Subscription Purchase

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_SponsoredPasswordManager_ReturnsCorrectTaxAmounts()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = true
            }
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 500 }],
            Total = 5500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(55.00m, total);

        // Verify the correct Stripe API call for sponsored subscription
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2021-family-for-enterprise-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 1 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_StandaloneSecretsManager_ReturnsCorrectTaxAmounts()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            SecretsManager = new OrganizationSubscriptionPurchase.SecretsManagerSelections
            {
                Seats = 3,
                AdditionalServiceAccounts = 0,
                Standalone = true
            }
        };

        var billingAddress = new BillingAddress
        {
            Country = "CA",
            PostalCode = "K1A 0A6"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 750 }],
            Total = 8250
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(7.50m, tax);
        Assert.Equal(82.50m, total);

        // Verify the correct Stripe API call for standalone secrets manager
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-teams-org-seat-monthly" && item.Quantity == 5) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-teams-seat-monthly" && item.Quantity == 3) &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == CouponIDs.SecretsManagerStandalone));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_StandardPurchaseWithStorage_ReturnsCorrectTaxAmounts()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 10,
                AdditionalStorage = 5,
                Sponsored = false
            },
            SecretsManager = new OrganizationSubscriptionPurchase.SecretsManagerSelections
            {
                Seats = 8,
                AdditionalServiceAccounts = 3,
                Standalone = false
            }
        };

        var billingAddress = new BillingAddress
        {
            Country = "GB",
            PostalCode = "SW1A 1AA",
            TaxId = new TaxID("gb_vat", "123456789")
        };

        var plan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1200 }],
            Total = 12200
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(12.00m, tax);
        Assert.Equal(122.00m, total);

        // Verify the correct Stripe API call for comprehensive purchase with storage and service accounts
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.CustomerDetails.TaxIds.Count == 1 &&
            options.CustomerDetails.TaxIds[0].Type == "gb_vat" &&
            options.CustomerDetails.TaxIds[0].Value == "123456789" &&
            options.SubscriptionDetails.Items.Count == 4 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 10) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "storage-gb-annually" && item.Quantity == 5) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-annually" && item.Quantity == 8) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-service-account-2024-annually" && item.Quantity == 3) &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_FamiliesTier_NoSecretsManager_ReturnsCorrectTaxAmounts()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = false
            }
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "90210"
        };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify the correct Stripe API call for Families tier (non-seat-based plan)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "90210" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2020-families-org-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 6 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_SpanishNIFTaxId_AddsEUVATTaxId()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 15,
                AdditionalStorage = 0,
                Sponsored = false
            }
        };

        var billingAddress = new BillingAddress
        {
            Country = "ES",
            PostalCode = "28001",
            TaxId = new TaxID(TaxIdType.SpanishNIF, "12345678Z")
        };

        var plan = new EnterprisePlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 2100 }],
            Total = 12100
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(21.00m, tax);
        Assert.Equal(121.00m, total);

        // Verify the correct Stripe API call for Spanish NIF that adds both Spanish NIF and EU VAT tax IDs
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "ES" &&
            options.CustomerDetails.Address.PostalCode == "28001" &&
            options.CustomerDetails.TaxIds.Count == 2 &&
            options.CustomerDetails.TaxIds.Any(t => t.Type == TaxIdType.SpanishNIF && t.Value == "12345678Z") &&
            options.CustomerDetails.TaxIds.Any(t => t.Type == TaxIdType.EUVAT && t.Value == "ES12345678Z") &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-enterprise-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 15 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_TeamsWithCoupon_IgnoresCoupon()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["TEST_COUPON_20"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify coupon is ignored for Teams plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_EnterpriseWithCoupon_IgnoresCoupon()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 10,
                AdditionalStorage = 5,
                Sponsored = false
            },
            SecretsManager = new OrganizationSubscriptionPurchase.SecretsManagerSelections
            {
                Seats = 8,
                AdditionalServiceAccounts = 2,
                Standalone = false
            },
            Coupons = ["ENTERPRISE_DISCOUNT_15"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "CA",
            PostalCode = "K1A 0A6"
        };

        var plan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1200 }],
            Total = 13200
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(12.00m, tax);
        Assert.Equal(132.00m, total);

        // Verify coupon is ignored for Enterprise plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.SubscriptionDetails.Items.Count == 4 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 10) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "storage-gb-annually" && item.Quantity == 5) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-annually" && item.Quantity == 8) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-service-account-2024-annually" && item.Quantity == 2) &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_SponsoredPlanWithCoupon_IgnoresCoupon()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = true
            },
            Coupons = ["TEST_COUPON_IGNORED"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 500 }],
            Total = 5500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(55.00m, total);

        // Verify coupon is ignored for sponsored plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2021-family-for-enterprise-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 1 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_StandaloneSecretsManagerWithCoupon_UsesSystemCoupon()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            SecretsManager = new OrganizationSubscriptionPurchase.SecretsManagerSelections
            {
                Seats = 3,
                AdditionalServiceAccounts = 0,
                Standalone = true
            },
            Coupons = ["USER_COUPON_IGNORED"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "CA",
            PostalCode = "K1A 0A6"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 750 }],
            Total = 8250
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(7.50m, tax);
        Assert.Equal(82.50m, total);

        // Verify user coupon is ignored and system coupon (SecretsManagerStandalone) is used instead
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-teams-org-seat-monthly" && item.Quantity == 5) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-teams-seat-monthly" && item.Quantity == 3) &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == CouponIDs.SecretsManagerStandalone));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_EmptyStringCoupon_TreatedAsNull()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = null
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify empty string coupon is treated same as null (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_NullCoupon_NoDiscountApplied()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            }
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify null coupon results in no discounts applied
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_WhitespaceOnlyCoupon_TreatedAsNull()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["   "]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        // Whitespace-only strings are now trimmed and treated as null/empty, so no discount is applied
        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify whitespace-only coupon is treated as null (no discount applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_TeamsWithCouponWithWhitespace_IgnoresCoupon()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["  TEST_COUPON_20  "]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify coupon is ignored for Teams plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_TeamsWithLongCoupon_IgnoresCoupon()
    {
        // Very long coupon string (200 characters)
        var longCoupon = new string('A', 200);

        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = [longCoupon]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify coupon is ignored for Teams plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_TeamsWithSpecialCharactersCoupon_IgnoresCoupon()
    {
        // Coupon with special characters (hyphens, underscores, numbers are common in Stripe coupon IDs)
        var specialCoupon = "TEST-COUPON_2024-50%OFF";

        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = [specialCoupon]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify coupon is ignored for Teams plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_TeamsWithUnicodeCoupon_IgnoresCoupon()
    {
        // Coupon with unicode characters (though unlikely for real Stripe coupons, tests edge case)
        var unicodeCoupon = "TEST-COUPON-2024";

        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = [unicodeCoupon]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify coupon is ignored for Teams plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    #endregion

    #region Subscription Plan Change

    [Fact]
    public async Task Run_OrganizationPlanChange_FreeOrganizationToTeams_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.Free,
            UseSecretsManager = false
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 120 }],
            Total = 1320
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(1.20m, tax);
        Assert.Equal(13.20m, total);

        // Verify the correct Stripe API call for free organization upgrade to Teams
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 2 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationPlanChange_FreeOrganizationToFamilies_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.Free,
            UseSecretsManager = true
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "CA",
            PostalCode = "K1A 0A6"
        };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 400 }],
            Total = 4400
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(4.00m, tax);
        Assert.Equal(44.00m, total);

        // Verify the correct Stripe API call for free organization upgrade to Families (no SM for Families)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2020-families-org-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 1 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationPlanChange_FamiliesOrganizationToTeams_UsesOrganizationSeats()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false,
            Seats = 6
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "10012"
        };

        var currentPlan = new FamiliesPlan();
        var newPlan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2020-families-org-annually" }, Quantity = 1 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax
            {
                Amount = 900
            }
            ],
            Total = 9900
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(9.00m, tax);
        Assert.Equal(99.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "10012" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 6 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationPlanChange_FamiliesOrganizationToEnterprise_UsesOrganizationSeats()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false,
            Seats = 6
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "10012"
        };

        var currentPlan = new FamiliesPlan();
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2020-families-org-annually" }, Quantity = 1 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax
            {
                Amount = 1200
            }
            ],
            Total = 13200
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(12.00m, tax);
        Assert.Equal(132.00m, total);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "10012" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-enterprise-org-seat-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 6 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationPlanChange_FreeOrganizationWithSecretsManagerToEnterprise_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.Free,
            UseSecretsManager = true
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "GB",
            PostalCode = "SW1A 1AA"
        };

        var plan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 800 }],
            Total = 8800
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(8.00m, tax);
        Assert.Equal(88.00m, total);

        // Verify the correct Stripe API call for free organization with SM to Enterprise
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 2) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-annually" && item.Quantity == 2) &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationPlanChange_ExistingSubscriptionUpgrade_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsMonthly,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = true
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "DE",
            PostalCode = "10115"
        };

        var currentPlan = new TeamsPlan(false);
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        // Mock existing subscription with items keyed by the CURRENT plan's price IDs, as a real
        // subscription is. The command re-prices each to the new plan's IDs in the preview.
        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2023-teams-org-seat-monthly" }, Quantity = 8 },
            new() { Price = new Price { Id = "storage-gb-monthly" }, Quantity = 3 },
            new() { Price = new Price { Id = "secrets-manager-teams-seat-monthly" }, Quantity = 5 },
            new() { Price = new Price { Id = "secrets-manager-service-account-2024-monthly" }, Quantity = 10 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1500 }],
            Total = 16500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(15.00m, tax);
        Assert.Equal(165.00m, total);

        // Verify the correct Stripe API call for existing subscription upgrade
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "DE" &&
            options.CustomerDetails.Address.PostalCode == "10115" &&
            options.SubscriptionDetails.Items.Count == 4 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 8) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "storage-gb-annually" && item.Quantity == 3) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-annually" && item.Quantity == 5) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-service-account-2024-annually" && item.Quantity == 10) &&
            options.Discounts == null));
    }

    // PM-37510 (T7): the plan-change preview copies the existing subscription's already-materialized
    // (grace-reduced) SM service-account quantity verbatim — no grace recompute happens here. A
    // migrated Enterprise org billed for only 20 accounts above its 200 free ceiling previews exactly
    // those 20.
    [Fact]
    public async Task Run_OrganizationPlanChange_MigratedOrg_CopiesGraceReducedServiceAccountQuantity()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = true
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress { Country = "US", PostalCode = "12345" };

        var plan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(plan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = plan.PasswordManager.StripeSeatPlanId }, Quantity = 10 },
            new() { Price = new Price { Id = plan.SecretsManager.StripeSeatPlanId }, Quantity = 5 },
            // Already grace-reduced: 220 accounts - 200 free ceiling => 20 billed.
            new() { Price = new Price { Id = plan.SecretsManager.StripeServiceAccountPlanId }, Quantity = 20 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice { TotalTaxes = [new InvoiceTotalTax { Amount = 0 }], Total = 0 };
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == plan.SecretsManager.StripeServiceAccountPlanId && item.Quantity == 20)));
    }

    // Regression test: upgrading from a flat-rate, non-seat-based plan (e.g. Teams Starter) has no
    // per-seat Password Manager line item on the subscription to read a quantity from, so the command
    // must fall back to the organization's occupied seat count instead of throwing a KeyNotFoundException.
    [Fact]
    public async Task Run_OrganizationPlanChange_TeamsStarterToTeams_UsesOrganizationSeats()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsStarter2023,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false,
            Seats = 10
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "10012"
        };

        var currentPlan = new TeamsStarterPlan2023();
        var newPlan = new Teams2023Plan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        // The flat-rate plan's subscription only contains its single, non-seat-based line item -
        // there is no per-seat price for the command to look up.
        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = currentPlan.PasswordManager.StripePlanId }, Quantity = 1 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 900 }],
            Total = 9900
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == newPlan.PasswordManager.StripeSeatPlanId &&
            options.SubscriptionDetails.Items[0].Quantity == organization.Seats));
    }

    // Regression test: if the organization's current plan is seat-based but its subscription does not
    // contain a line item matching that plan's price (a data discrepancy), the command should return a
    // BadRequest rather than throwing a KeyNotFoundException from the dictionary lookup.
    [Fact]
    public async Task Run_OrganizationPlanChange_SubscriptionMissingCurrentPlanPriceLineItem_ReturnsBadRequest()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsMonthly2023,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false,
            Seats = 5
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var currentPlan = new Teams2023Plan(false);
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        // The subscription does not contain a line item for the current plan's Password Manager price.
        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "some-other-unrelated-price" }, Quantity = 5 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal(
            "Your organization's subscription does not match its current plan. Please contact support for assistance.",
            badRequest.Response);

        await _stripeAdapter.DidNotReceive().CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>());
    }

    [Fact]
    public async Task Run_OrganizationPlanChange_ExistingSubscriptionWithDiscount_PreservesCoupon()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "90210"
        };

        var currentPlan = new TeamsPlan(true);
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        // Mock existing subscription with discount
        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2023-teams-org-seat-annually" }, Quantity = 5 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer
            {
                Discount = new Discount
                {
                    Coupon = new Coupon { Id = "EXISTING_DISCOUNT_50" }
                }
            }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 600 }],
            Total = 6600
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(6.00m, tax);
        Assert.Equal(66.00m, total);

        // Verify the correct Stripe API call preserves existing discount
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "90210" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-enterprise-org-seat-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "EXISTING_DISCOUNT_50"));
    }

    // PM-40440: genuine org coupons (complimentary PM, SM-standalone) attach at the subscription level, not the
    // customer. The preview must read subscription.Discounts too, or it over-quotes by dropping the coupon.
    [Fact]
    public async Task Run_OrganizationPlanChange_ExistingSubscriptionWithSubscriptionLevelDiscount_PreservesCoupon()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "90210"
        };

        var currentPlan = new TeamsPlan(true);
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2023-teams-org-seat-annually" }, Quantity = 5 }
        };

        // A genuine complimentary/SM-standalone coupon lives at the subscription level, with no customer discount.
        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null },
            Discounts = [new Discount { Coupon = new Coupon { Id = "COMPLIMENTARY_PM_100" } }]
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        // The subscription-level coupon is applied to the preview so Stripe nets it out of the total.
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "COMPLIMENTARY_PM_100"));
    }

    // PM-40440 guardrail: the schedule-derived migration coupon lives on the subscription SCHEDULE (Phase 2),
    // never on the live subscription. A migrating org (schedule present, no live discount) must still preview the
    // full target-plan price — reading only live discounts keeps the migration coupon out of the preview.
    [Fact]
    public async Task Run_OrganizationPlanChange_MigratingOrgWithScheduleCoupon_DoesNotApplyMigrationDiscount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsAnnually2020,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false,
            Seats = 10
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "90210"
        };

        var currentPlan = new Teams2020Plan(true);
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2020-teams-org-seat-annually" }, Quantity = 10 }
        };

        // Migrating org: the migration coupon is only on the subscription schedule's Phase 2; the live
        // subscription (Phase 1) carries no discount at the customer or subscription level.
        var subscription = new Subscription
        {
            Id = "sub_test123",
            ScheduleId = "sub_sched_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null },
            Discounts = []
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        // No discount is applied.
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts == null));

        // The preview must never fetch the schedule — the only place the migration coupon lives — which is what
        // keeps it out of the quote. Guards against a future regression that starts reading the schedule here.
        await _stripeAdapter.DidNotReceive().GetSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleGetOptions>());
    }

    [Fact]
    public async Task Run_OrganizationPlanChange_OrganizationWithoutGatewayIds_ReturnsBadRequest()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsMonthly,
            GatewayCustomerId = null,
            GatewaySubscriptionId = null
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Organization does not have a subscription.", badRequest.Response);

        // Verify no Stripe API calls were made
        await _stripeAdapter.DidNotReceive().CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>());
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    // PM-40440 regression: an SM-carrying Teams 2020 annual org upgrading to Enterprise annual must
    // still be quoted for its Secrets Manager seats and service accounts. The SM items are looked up
    // on the current subscription by the CURRENT plan's price IDs - which differ from the new plan's
    // across the tier boundary and the service-account 2024 bump - then re-priced at the new plan.
    // Before the fix these lookups used the new plan's IDs, missed, and dropped SM from the total.
    [Fact]
    public async Task Run_OrganizationPlanChange_Teams2020AnnualWithSecretsManagerToEnterprise_IncludesSecretsManager()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsAnnually2020,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = true,
            Seats = 10
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var currentPlan = new Teams2020Plan(true);
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        // A real Teams 2020 annual subscription with Secrets Manager: SM seats and (legacy) service
        // accounts are keyed by the current plan's price IDs.
        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2020-teams-org-seat-annually" }, Quantity = 10 },
            new() { Price = new Price { Id = "secrets-manager-teams-seat-annually" }, Quantity = 10 },
            new() { Price = new Price { Id = "secrets-manager-service-account-annually" }, Quantity = 5 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        // SM seats and service accounts are re-priced at the new (Enterprise annual) plan's IDs but keep
        // the current subscription's quantities.
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.SubscriptionDetails.Items.Count == 3 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 10) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-annually" && item.Quantity == 10) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-service-account-2024-annually" && item.Quantity == 5)));
    }

    // PM-40440 null guard: a Families org's current plan has no Secrets Manager definition
    // (Plan.SecretsManager is null), so the SM lookup must be guarded. Families -> Enterprise must not
    // throw and must add no SM line.
    [Fact]
    public async Task Run_OrganizationPlanChange_FamiliesToEnterprise_NoSecretsManagerLineOrThrow()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = true,
            Seats = 6
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var currentPlan = new FamiliesPlan();
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2020-families-org-annually" }, Quantity = 1 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items.All(item => !item.Price.Contains("secrets-manager"))));
    }

    // PM-40440: the SM seat ID is per-tier, so the same drop happens on a monthly tier change. A
    // Teams monthly -> Enterprise monthly upgrade must also carry SM through the preview.
    [Fact]
    public async Task Run_OrganizationPlanChange_TeamsMonthlyWithSecretsManagerToEnterpriseMonthly_IncludesSecretsManager()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsMonthly,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = true
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Monthly
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var currentPlan = new TeamsPlan(false);
        var newPlan = new EnterprisePlan(false);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2023-teams-org-seat-monthly" }, Quantity = 8 },
            new() { Price = new Price { Id = "secrets-manager-teams-seat-monthly" }, Quantity = 4 },
            new() { Price = new Price { Id = "secrets-manager-service-account-2024-monthly" }, Quantity = 6 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.SubscriptionDetails.Items.Count == 3 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-seat-monthly" && item.Quantity == 8) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-monthly" && item.Quantity == 4) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-service-account-2024-monthly" && item.Quantity == 6)));
    }

    // PM-40440: an org with no Secrets Manager items on its subscription still previews without an SM
    // line (behavior preserved for non-SM orgs).
    [Fact]
    public async Task Run_OrganizationPlanChange_TeamsAnnualWithoutSecretsManagerToEnterprise_NoSecretsManagerLine()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var currentPlan = new TeamsPlan(true);
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2023-teams-org-seat-annually" }, Quantity = 5 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 5) &&
            options.SubscriptionDetails.Items.All(item => !item.Price.Contains("secrets-manager"))));
    }

    // PM-40440 (storage): storage ids are not shared across all plans - Families uses
    // personal-storage-gb-* while org plans use storage-gb-*. Looking up existing storage by the NEW
    // plan's id misses on a Families -> Enterprise upgrade and drops it from the preview. Matching by
    // the CURRENT plan's id and re-pricing at the new plan carries the extra storage through.
    [Fact]
    public async Task Run_OrganizationPlanChange_FamiliesWithStorageToEnterprise_IncludesStorage()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123",
            UseSecretsManager = false,
            Seats = 6
        };

        var planChange = new OrganizationSubscriptionPlanChange
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var currentPlan = new FamiliesPlan();
        var newPlan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(planChange.PlanType).Returns(newPlan);

        // A real Families subscription with extra storage: storage is keyed by the Families (personal)
        // storage id, which differs from the org plan's storage id.
        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2020-families-org-annually" }, Quantity = 1 },
            new() { Price = new Price { Id = "personal-storage-gb-annually" }, Quantity = 5 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);

        // Storage is re-priced at the new (Enterprise) plan's id but keeps the current quantity; the
        // Families personal-storage id must not leak into the preview.
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "storage-gb-annually" && item.Quantity == 5) &&
            options.SubscriptionDetails.Items.All(item => item.Price != "personal-storage-gb-annually")));
    }

    #endregion

    #region Subscription Update

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_PasswordManagerSeatsOnly_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsMonthly,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            PasswordManager = new OrganizationSubscriptionUpdate.PasswordManagerSelections
            {
                Seats = 10,
                AdditionalStorage = null
            }
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "12345" },
            Discount = null,
            TaxIds = null
        };

        var subscription = new Subscription
        {
            Customer = customer
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 600 }],
            Total = 6600
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(6.00m, tax);
        Assert.Equal(66.00m, total);

        // Verify the correct Stripe API call for PM seats only
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 10 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_PasswordManagerWithStorage_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            PasswordManager = new OrganizationSubscriptionUpdate.PasswordManagerSelections
            {
                Seats = 15,
                AdditionalStorage = 5
            }
        };

        var plan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var customer = new Customer
        {
            Address = new Address { Country = "CA", PostalCode = "K1A 0A6" },
            Discount = null,
            TaxIds = null
        };

        var subscription = new Subscription
        {
            Customer = customer
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1200 }],
            Total = 13200
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(12.00m, tax);
        Assert.Equal(132.00m, total);

        // Verify the correct Stripe API call for PM seats + storage
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 15) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "storage-gb-annually" && item.Quantity == 5) &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_SecretsManagerOnly_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            SecretsManager = new OrganizationSubscriptionUpdate.SecretsManagerSelections
            {
                Seats = 8,
                AdditionalServiceAccounts = null
            }
        };

        var plan = new TeamsPlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var customer = new Customer
        {
            Address = new Address { Country = "DE", PostalCode = "10115" },
            Discount = null,
            TaxIds = null
        };

        var subscription = new Subscription
        {
            Customer = customer
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 800 }],
            Total = 8800
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(8.00m, tax);
        Assert.Equal(88.00m, total);

        // Verify the correct Stripe API call for SM seats only
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "DE" &&
            options.CustomerDetails.Address.PostalCode == "10115" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "secrets-manager-teams-seat-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 8 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_SecretsManagerWithServiceAccounts_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseMonthly,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            SecretsManager = new OrganizationSubscriptionUpdate.SecretsManagerSelections
            {
                Seats = 12,
                AdditionalServiceAccounts = 20
            }
        };

        var plan = new EnterprisePlan(false);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var customer = new Customer
        {
            Address = new Address { Country = "GB", PostalCode = "SW1A 1AA" },
            Discount = null,
            TaxIds = new StripeList<TaxId>
            {
                Data = [new TaxId { Type = "gb_vat", Value = "GB123456789" }]
            }
        };

        var subscription = new Subscription
        {
            Customer = customer
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1500 }],
            Total = 16500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(15.00m, tax);
        Assert.Equal(165.00m, total);

        // Verify the correct Stripe API call for SM seats + service accounts with tax ID
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.CustomerDetails.TaxIds.Count == 1 &&
            options.CustomerDetails.TaxIds[0].Type == "gb_vat" &&
            options.CustomerDetails.TaxIds[0].Value == "GB123456789" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-monthly" && item.Quantity == 12) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-service-account-2024-monthly" && item.Quantity == 20) &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_ComprehensiveUpdate_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            PasswordManager = new OrganizationSubscriptionUpdate.PasswordManagerSelections
            {
                Seats = 25,
                AdditionalStorage = 10
            },
            SecretsManager = new OrganizationSubscriptionUpdate.SecretsManagerSelections
            {
                Seats = 15,
                AdditionalServiceAccounts = 30
            }
        };

        var plan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var customer = new Customer
        {
            Address = new Address { Country = "ES", PostalCode = "28001" },
            Discount = new Discount
            {
                Coupon = new Coupon { Id = "ENTERPRISE_DISCOUNT_20" }
            },
            TaxIds = new StripeList<TaxId>
            {
                Data = [new TaxId { Type = TaxIdType.SpanishNIF, Value = "12345678Z" }]
            }
        };

        var subscription = new Subscription
        {
            Customer = customer
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 2500 }],
            Total = 27500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(25.00m, tax);
        Assert.Equal(275.00m, total);

        // Verify the correct Stripe API call for comprehensive update with discount and Spanish tax ID
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "ES" &&
            options.CustomerDetails.Address.PostalCode == "28001" &&
            options.CustomerDetails.TaxIds.Count == 2 &&
            options.CustomerDetails.TaxIds.Any(t => t.Type == TaxIdType.SpanishNIF && t.Value == "12345678Z") &&
            options.CustomerDetails.TaxIds.Any(t => t.Type == TaxIdType.EUVAT && t.Value == "ES12345678Z") &&
            options.SubscriptionDetails.Items.Count == 4 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2023-enterprise-org-seat-annually" && item.Quantity == 25) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "storage-gb-annually" && item.Quantity == 10) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-enterprise-seat-annually" && item.Quantity == 15) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "secrets-manager-service-account-2024-annually" && item.Quantity == 30) &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "ENTERPRISE_DISCOUNT_20"));
    }

    // PM-40440: the update-overload preview must also read subscription-level discounts, not just the customer
    // discount — same over-quote bug as the plan-change path (the fix is applied to both overloads).
    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_SubscriptionLevelDiscount_PreservesCoupon()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            PasswordManager = new OrganizationSubscriptionUpdate.PasswordManagerSelections
            {
                Seats = 25
            }
        };

        var plan = new EnterprisePlan(true);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        // A genuine coupon lives at the subscription level, with no customer-level discount.
        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "US", PostalCode = "90210" },
                Discount = null
            },
            Discounts = [new Discount { Coupon = new Coupon { Id = "COMPLIMENTARY_PM_100" } }]
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 0
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);

        // The subscription-level coupon is applied to the preview so Stripe nets it out of the total.
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "COMPLIMENTARY_PM_100"));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_FamiliesTierPersonalUsage_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.FamiliesAnnually,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            PasswordManager = new OrganizationSubscriptionUpdate.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 2
            }
        };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var customer = new Customer
        {
            Address = new Address { Country = "AU", PostalCode = "2000" },
            Discount = null,
            TaxIds = null
        };

        var subscription = new Subscription
        {
            Customer = customer
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 500 }],
            Total = 5500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(55.00m, total);

        // Verify the correct Stripe API call for Families tier (personal usage, no business tax exemption)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "AU" &&
            options.CustomerDetails.Address.PostalCode == "2000" &&
            options.SubscriptionDetails.Items.Count == 2 &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "2020-families-org-annually" && item.Quantity == 6) &&
            options.SubscriptionDetails.Items.Any(item =>
                item.Price == "personal-storage-gb-annually" && item.Quantity == 2) &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_OrganizationWithoutGatewayIds_ReturnsBadRequest()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsMonthly,
            GatewayCustomerId = null,
            GatewaySubscriptionId = null
        };

        var update = new OrganizationSubscriptionUpdate
        {
            PasswordManager = new OrganizationSubscriptionUpdate.PasswordManagerSelections
            {
                Seats = 5
            }
        };

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Organization does not have a subscription.", badRequest.Response);

        // Verify no Stripe API calls were made
        await _stripeAdapter.DidNotReceive().CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>());
        await _stripeAdapter.DidNotReceive().GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionUpdate_ZeroValuesExcluded_ReturnsCorrectTaxAmounts()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = PlanType.TeamsMonthly,
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        };

        var update = new OrganizationSubscriptionUpdate
        {
            PasswordManager = new OrganizationSubscriptionUpdate.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0  // Should be excluded
            },
            SecretsManager = new OrganizationSubscriptionUpdate.SecretsManagerSelections
            {
                Seats = 0,  // Should be excluded entirely (including service accounts)
                AdditionalServiceAccounts = 10
            }
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var customer = new Customer
        {
            Address = new Address { Country = "US", PostalCode = "90210" },
            Discount = null,
            TaxIds = null
        };

        var subscription = new Subscription
        {
            Customer = customer
        };

        _stripeAdapter.GetSubscriptionAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify only PM seats are included (storage=0 excluded, SM seats=0 so entire SM excluded)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "90210" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    #endregion

    #region Coupon Validation

    [Fact]
    public async Task Run_FamiliesOrganizationWithValidCoupon_ValidatesCouponAndAppliesDiscount()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["VALID_FAMILIES_DISCOUNT"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        _subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
            _user,
            Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "VALID_FAMILIES_DISCOUNT" })),
            DiscountTierType.Families).Returns(true);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _subscriptionDiscountService.Received(1).ValidateDiscountEligibilityForUserAsync(
            _user,
            Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "VALID_FAMILIES_DISCOUNT" })),
            DiscountTierType.Families);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "VALID_FAMILIES_DISCOUNT"));
    }

    [Fact]
    public async Task Run_FamiliesOrganizationWithInvalidCoupon_ProceedsWithoutDiscount()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["INVALID_COUPON"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        _subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
            _user,
            Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "INVALID_COUPON" })),
            DiscountTierType.Families).Returns(false);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        await _subscriptionDiscountService.Received(1).ValidateDiscountEligibilityForUserAsync(
            _user,
            Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "INVALID_COUPON" })),
            DiscountTierType.Families);

        // Verify invalid coupon is silently ignored (no discount applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2020-families-org-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 6 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_TeamsOrganizationWithCoupon_IgnoresCoupon()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["TEAMS_COUPON"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new TeamsPlan(isAnnual: false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify coupon validation was NOT called for Teams (only Families plans use coupons)
        await _subscriptionDiscountService.DidNotReceive().ValidateDiscountEligibilityForUserAsync(
            Arg.Any<User>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<DiscountTierType>());

        // Verify coupon is ignored for Teams plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_EnterpriseOrganizationWithCoupon_IgnoresCoupon()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Enterprise,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 10,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["ENTERPRISE_COUPON"]
        };

        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var plan = new EnterprisePlan(isAnnual: true);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 600 }],
            Total = 6600
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(6.00m, tax);
        Assert.Equal(66.00m, total);

        // Verify coupon validation was NOT called for Enterprise (only Families plans use coupons)
        await _subscriptionDiscountService.DidNotReceive().ValidateDiscountEligibilityForUserAsync(
            Arg.Any<User>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<DiscountTierType>());

        // Verify coupon is ignored for Enterprise plans (no discounts applied)
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-enterprise-org-seat-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 10 &&
            options.Discounts == null));
    }

    #endregion

    #region Multi-coupon support

    [Fact]
    public async Task Run_WithMultipleValidCoupons_AppliesBothToInvoicePreview()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["COUPON_ONE", "COUPON_TWO"]
        };

        var billingAddress = new BillingAddress { Country = "US", PostalCode = "12345" };
        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        _subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
            _user,
            Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "COUPON_ONE", "COUPON_TWO" })),
            DiscountTierType.Families).Returns(true);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 200 }],
            Total = 2200
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts != null &&
            options.Discounts.Count == 2 &&
            options.Discounts.Any(d => d.Coupon == "COUPON_ONE") &&
            options.Discounts.Any(d => d.Coupon == "COUPON_TWO")));
    }

    [Fact]
    public async Task Run_WithStandaloneSecretsManagerAndCoupons_IgnoresUserCoupons()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 5,
                AdditionalStorage = 0,
                Sponsored = false
            },
            SecretsManager = new OrganizationSubscriptionPurchase.SecretsManagerSelections
            {
                Seats = 3,
                AdditionalServiceAccounts = 0,
                Standalone = true
            },
            Coupons = ["COUPON_ONE", "COUPON_TWO"]
        };

        var billingAddress = new BillingAddress { Country = "US", PostalCode = "12345" };
        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 500 }],
            Total = 5500
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);

        // User coupons ignored; system coupon applied for standalone SM
        await _subscriptionDiscountService.DidNotReceive().ValidateDiscountEligibilityForUserAsync(
            Arg.Any<User>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<DiscountTierType>());

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == CouponIDs.SecretsManagerStandalone));
    }

    [Fact]
    public async Task Run_WithMixedValidAndInvalidCoupons_SkipsAllDiscounts()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = false
            },
            Coupons = ["VALID_COUPON", "INVALID_COUPON"]
        };

        var billingAddress = new BillingAddress { Country = "US", PostalCode = "12345" };
        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        _subscriptionDiscountService.ValidateDiscountEligibilityForUserAsync(
            _user,
            Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "VALID_COUPON", "INVALID_COUPON" })),
            DiscountTierType.Families).Returns(false);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(_user, purchase, billingAddress);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.Discounts == null || options.Discounts.Count == 0));
    }

    #endregion

    #region Feature flag

    [Fact]
    public async Task Run_BusinessUse_DoesNotSetCustomerDetailsTaxExempt()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Teams,
            Cadence = PlanCadenceType.Monthly,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 3,
                AdditionalStorage = 0,
                Sponsored = false
            }
        };

        var billingAddress = new BillingAddress { Country = "DE", PostalCode = "10115" };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice { TotalTaxes = [new InvoiceTotalTax { Amount = 0 }], Total = 2700 });

        await _command.Run(_user, purchase, billingAddress);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.CustomerDetails.TaxExempt == null));
    }

    [Fact]
    public async Task Run_FamiliesTier_DoesNotSetCustomerDetailsTaxExempt()
    {
        var purchase = new OrganizationSubscriptionPurchase
        {
            Tier = ProductTierType.Families,
            Cadence = PlanCadenceType.Annually,
            PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
            {
                Seats = 6,
                AdditionalStorage = 0,
                Sponsored = false
            }
        };

        var billingAddress = new BillingAddress { Country = "US", PostalCode = "12345" };

        var plan = new FamiliesPlan();
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(new Invoice { TotalTaxes = [new InvoiceTotalTax { Amount = 0 }], Total = 4000 });

        await _command.Run(_user, purchase, billingAddress);

        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.CustomerDetails.TaxExempt == null));
    }

    #endregion
}
