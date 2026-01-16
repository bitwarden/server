using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class PreviewPremiumUpgradeProrationCommandTests
{
    private readonly ILogger<PreviewPremiumUpgradeProrationCommand> _logger = Substitute.For<ILogger<PreviewPremiumUpgradeProrationCommand>>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly PreviewPremiumUpgradeProrationCommand _command;

    public PreviewPremiumUpgradeProrationCommandTests()
    {
        _command = new PreviewPremiumUpgradeProrationCommand(
            _logger,
            _pricingClient,
            _stripeAdapter);
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithoutPremium_ReturnsBadRequest(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = false;

        // Act
        var result = await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have an active Premium subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_UserWithoutGatewaySubscriptionId_ReturnsBadRequest(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = null;

        // Act
        var result = await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have an active Premium subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidUpgrade_ReturnsProrationAmounts(User user, BillingAddress billingAddress)
    {
        // Arrange - Setup valid Premium user
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        // Setup Premium plans
        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };

        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        // Setup current Stripe subscription
        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer
            {
                Id = "cus_123",
                Discount = null
            },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = "si_premium",
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            }
        };

        // Setup target organization plan
        var targetPlan = new TeamsPlan(isAnnual: true);

        // Setup invoice preview response
        var invoice = new Invoice
        {
            Total = 5000, // $50.00
            TotalTaxes = new List<InvoiceTotalTax>
            {
                new() { Amount = 500 } // $5.00
            }
        };

        // Configure mocks
        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync(
            "sub_123",
            Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        var result = await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        var (tax, total, credit) = result.AsT0;
        Assert.Equal(5.00m, tax);
        Assert.Equal(50.00m, total);
        Assert.Equal(0m, credit);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidUpgrade_ExtractsProrationCredit(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };
        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" } }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        // Invoice with negative line item (proration credit)
        var invoice = new Invoice
        {
            Total = 4000, // $40.00
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 400 } }, // $4.00
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = new List<InvoiceLineItem>
                {
                    new() { Amount = -1000 },  // -$10.00 credit from unused Premium
                    new() { Amount = 5000 }    // $50.00 for new plan
                }
            }
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        var result = await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        var (tax, total, credit) = result.AsT0;
        Assert.Equal(4.00m, tax);
        Assert.Equal(40.00m, total);
        Assert.Equal(10.00m, credit);  // Proration credit
    }

    [Theory, BitAutoData]
    public async Task Run_ValidUpgrade_AlwaysUsesOneSeat(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };
        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" } }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } }
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert - Verify that the subscription item quantity is always 1
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Any(item =>
                    item.Price == targetPlan.PasswordManager.StripeSeatPlanId &&
                    item.Quantity == 1)));
    }

    [Theory]
    [InlineData(ProductTierType.Families, PlanType.FamiliesAnnually)]
    [InlineData(ProductTierType.Teams, PlanType.TeamsAnnually)]
    [InlineData(ProductTierType.Enterprise, PlanType.EnterpriseAnnually)]
    public async Task Run_ProductTierTypeConversion_MapsToCorrectPlanType(
        ProductTierType productTierType,
        PlanType expectedPlanType)
    {
        // Arrange
        var user = new User
        {
            Premium = true,
            GatewaySubscriptionId = "sub_123",
            GatewayCustomerId = "cus_123"
        };
        var billingAddress = new BillingAddress
        {
            Country = "US",
            PostalCode = "12345"
        };

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };
        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" } }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } }
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(expectedPlanType).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, productTierType, billingAddress);

        // Assert - Verify that the correct PlanType was used
        await _pricingClient.Received(1).GetPlanOrThrow(expectedPlanType);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidUpgrade_DeletesPremiumSubscriptionItems(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };
        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_password_manager", Price = new Price { Id = "premium-annually" } },
                    new() { Id = "si_storage", Price = new Price { Id = "storage-gb-annually" } }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } }
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert - Verify both password manager and storage items are marked as deleted
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Any(item =>
                    item.Id == "si_password_manager" && item.Deleted == true) &&
                options.SubscriptionDetails.Items.Any(item =>
                    item.Id == "si_storage" && item.Deleted == true)));
    }

    [Theory, BitAutoData]
    public async Task Run_NonSeatBasedPlan_UsesStripePlanId(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };
        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" } }
                }
            }
        };

        var targetPlan = new FamiliesPlan(); // families is non seat based

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } }
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, ProductTierType.Families, billingAddress);

        // Assert - Verify non-seat-based plan uses StripePlanId with quantity 1
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Any(item =>
                    item.Price == targetPlan.PasswordManager.StripePlanId &&
                    item.Quantity == 1)));
    }

    [Theory, BitAutoData]
    public async Task Run_ValidUpgrade_CreatesCorrectInvoicePreviewOptions(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";
        billingAddress.Country = "US";
        billingAddress.PostalCode = "12345";

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };
        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" } }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } }
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert - Verify all invoice preview options are correct
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.AutomaticTax.Enabled == true &&
                options.Customer == "cus_123" &&
                options.Subscription == "sub_123" &&
                options.CustomerDetails.Address.Country == "US" &&
                options.CustomerDetails.Address.PostalCode == "12345" &&
                options.SubscriptionDetails.ProrationBehavior == "always_invoice"));
    }

    [Theory, BitAutoData]
    public async Task Run_TeamsStarterTierType_ReturnsBadRequest(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        // Act
        var result = await _command.Run(user, ProductTierType.TeamsStarter, billingAddress);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Cannot upgrade Premium subscription to TeamsStarter plan.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_SeatBasedPlan_UsesStripeSeatPlanId(User user, BillingAddress billingAddress)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        var premiumPlan = new PremiumPlan
        {
            Name = "Premium",
            Available = true,
            LegacyYear = null,
            Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "premium-annually",
                Price = 10m,
                Provided = 1
            },
            Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
            {
                StripePriceId = "storage-gb-annually",
                Price = 4m,
                Provided = 1
            }
        };
        var premiumPlans = new List<PremiumPlan> { premiumPlan };

        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" } }
                }
            }
        };

        // Use Teams which is seat-based
        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } }
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, ProductTierType.Teams, billingAddress);

        // Assert - Verify seat-based plan uses StripeSeatPlanId with quantity 1
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Any(item =>
                    item.Price == targetPlan.PasswordManager.StripeSeatPlanId &&
                    item.Quantity == 1)));
    }
}
