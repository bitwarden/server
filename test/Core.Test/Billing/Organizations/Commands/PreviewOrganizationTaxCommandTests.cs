using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Services;
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
    private readonly PreviewOrganizationTaxCommand _command;

    public PreviewOrganizationTaxCommandTests()
    {
        _command = new PreviewOrganizationTaxCommand(_logger, _pricingClient, _stripeAdapter);
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(55.00m, total);

        // Verify the correct Stripe API call for sponsored subscription
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(7.50m, tax);
        Assert.Equal(82.50m, total);

        // Verify the correct Stripe API call for standalone secrets manager
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(12.00m, tax);
        Assert.Equal(122.00m, total);

        // Verify the correct Stripe API call for comprehensive purchase with storage and service accounts
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify the correct Stripe API call for Families tier (non-seat-based plan)
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "90210" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2020-families-org-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 6 &&
            options.Discounts == null));
    }

    [Fact]
    public async Task Run_OrganizationSubscriptionPurchase_BusinessUseNonUSCountry_UsesTaxExemptReverse()
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

        var billingAddress = new BillingAddress
        {
            Country = "DE",
            PostalCode = "10115"
        };

        var plan = new TeamsPlan(false);
        _pricingClient.GetPlanOrThrow(purchase.PlanType).Returns(plan);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 0 }],
            Total = 2700
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(0.00m, tax);
        Assert.Equal(27.00m, total);

        // Verify the correct Stripe API call for business use in non-US country (tax exempt reverse)
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "DE" &&
            options.CustomerDetails.Address.PostalCode == "10115" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 3 &&
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(purchase, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(21.00m, tax);
        Assert.Equal(121.00m, total);

        // Verify the correct Stripe API call for Spanish NIF that adds both Spanish NIF and EU VAT tax IDs
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "ES" &&
            options.CustomerDetails.Address.PostalCode == "28001" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
            options.CustomerDetails.TaxIds.Count == 2 &&
            options.CustomerDetails.TaxIds.Any(t => t.Type == TaxIdType.SpanishNIF && t.Value == "12345678Z") &&
            options.CustomerDetails.TaxIds.Any(t => t.Type == TaxIdType.EUVAT && t.Value == "ES12345678Z") &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-enterprise-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 15 &&
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(1.20m, tax);
        Assert.Equal(13.20m, total);

        // Verify the correct Stripe API call for free organization upgrade to Teams
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(4.00m, tax);
        Assert.Equal(44.00m, total);

        // Verify the correct Stripe API call for free organization upgrade to Families (no SM for Families)
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax
            {
                Amount = 900
            }
            ],
            Total = 9900
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(9.00m, tax);
        Assert.Equal(99.00m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "10012" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax
            {
                Amount = 1200
            }
            ],
            Total = 13200
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(12.00m, tax);
        Assert.Equal(132.00m, total);

        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "10012" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
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

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(8.00m, tax);
        Assert.Equal(88.00m, total);

        // Verify the correct Stripe API call for free organization with SM to Enterprise
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        // Mock existing subscription with items - using NEW plan IDs since command looks for new plan prices
        var subscriptionItems = new List<SubscriptionItem>
        {
            new() { Price = new Price { Id = "2023-teams-org-seat-monthly" }, Quantity = 8 },
            new() { Price = new Price { Id = "storage-gb-annually" }, Quantity = 3 },
            new() { Price = new Price { Id = "secrets-manager-enterprise-seat-annually" }, Quantity = 5 },
            new() { Price = new Price { Id = "secrets-manager-service-account-2024-annually" }, Quantity = 10 }
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Items = new StripeList<SubscriptionItem> { Data = subscriptionItems },
            Customer = new Customer { Discount = null }
        };

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1500 }],
            Total = 16500
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(15.00m, tax);
        Assert.Equal(165.00m, total);

        // Verify the correct Stripe API call for existing subscription upgrade
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "DE" &&
            options.CustomerDetails.Address.PostalCode == "10115" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 600 }],
            Total = 6600
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, planChange, billingAddress);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(6.00m, tax);
        Assert.Equal(66.00m, total);

        // Verify the correct Stripe API call preserves existing discount
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "90210" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-enterprise-org-seat-annually" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts != null &&
            options.Discounts.Count == 1 &&
            options.Discounts[0].Coupon == "EXISTING_DISCOUNT_50"));
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
        await _stripeAdapter.DidNotReceive().InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>());
        await _stripeAdapter.DidNotReceive().SubscriptionGetAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 600 }],
            Total = 6600
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(6.00m, tax);
        Assert.Equal(66.00m, total);

        // Verify the correct Stripe API call for PM seats only
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "12345" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1200 }],
            Total = 13200
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(12.00m, tax);
        Assert.Equal(132.00m, total);

        // Verify the correct Stripe API call for PM seats + storage
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "CA" &&
            options.CustomerDetails.Address.PostalCode == "K1A 0A6" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 800 }],
            Total = 8800
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(8.00m, tax);
        Assert.Equal(88.00m, total);

        // Verify the correct Stripe API call for SM seats only
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "DE" &&
            options.CustomerDetails.Address.PostalCode == "10115" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 1500 }],
            Total = 16500
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(15.00m, tax);
        Assert.Equal(165.00m, total);

        // Verify the correct Stripe API call for SM seats + service accounts with tax ID
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "GB" &&
            options.CustomerDetails.Address.PostalCode == "SW1A 1AA" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 2500 }],
            Total = 27500
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(25.00m, tax);
        Assert.Equal(275.00m, total);

        // Verify the correct Stripe API call for comprehensive update with discount and Spanish tax ID
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "ES" &&
            options.CustomerDetails.Address.PostalCode == "28001" &&
            options.CustomerDetails.TaxExempt == TaxExempt.Reverse &&
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 500 }],
            Total = 5500
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(55.00m, total);

        // Verify the correct Stripe API call for Families tier (personal usage, no business tax exemption)
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "AU" &&
            options.CustomerDetails.Address.PostalCode == "2000" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
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
        await _stripeAdapter.DidNotReceive().InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>());
        await _stripeAdapter.DidNotReceive().SubscriptionGetAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
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

        _stripeAdapter.SubscriptionGetAsync("sub_test123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        var invoice = new Invoice
        {
            TotalTaxes = [new InvoiceTotalTax { Amount = 300 }],
            Total = 3300
        };

        _stripeAdapter.InvoiceCreatePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var result = await _command.Run(organization, update);

        Assert.True(result.IsT0);
        var (tax, total) = result.AsT0;
        Assert.Equal(3.00m, tax);
        Assert.Equal(33.00m, total);

        // Verify only PM seats are included (storage=0 excluded, SM seats=0 so entire SM excluded)
        await _stripeAdapter.Received(1).InvoiceCreatePreviewAsync(Arg.Is<InvoiceCreatePreviewOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.Currency == "usd" &&
            options.CustomerDetails.Address.Country == "US" &&
            options.CustomerDetails.Address.PostalCode == "90210" &&
            options.CustomerDetails.TaxExempt == TaxExempt.None &&
            options.SubscriptionDetails.Items.Count == 1 &&
            options.SubscriptionDetails.Items[0].Price == "2023-teams-org-seat-monthly" &&
            options.SubscriptionDetails.Items[0].Quantity == 5 &&
            options.Discounts == null));
    }

    #endregion
}
