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
        var result = await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

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
        var result = await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

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
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentPeriodEnd = now.AddMonths(6);
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
                        Price = new Price { Id = "premium-annually" },
                        CurrentPeriodEnd = currentPeriodEnd
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
            },
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = new List<InvoiceLineItem>
                {
                    new() { Amount = 5000 }  // $50.00 for new plan
                }
            },
            PeriodEnd = now
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
        var result = await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        var proration = result.AsT0;
        Assert.Equal(50.00m, proration.NewPlanProratedAmount);
        Assert.Equal(0m, proration.Credit);
        Assert.Equal(5.00m, proration.Tax);
        Assert.Equal(50.00m, proration.Total);
        Assert.Equal(6, proration.NewPlanProratedMonths); // 6 months remaining
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

        // Use fixed time to avoid DateTime.UtcNow differences
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentPeriodEnd = now.AddDays(45); // 1.5 months ~ 2 months rounded
        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = currentPeriodEnd }
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
            },
            PeriodEnd = now
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        var proration = result.AsT0;
        Assert.Equal(50.00m, proration.NewPlanProratedAmount);
        Assert.Equal(10.00m, proration.Credit);  // Proration credit
        Assert.Equal(4.00m, proration.Tax);
        Assert.Equal(40.00m, proration.Total);
        Assert.Equal(2, proration.NewPlanProratedMonths); // 45 days rounds to 2 months
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
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1) }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } },
            Lines = new StripeList<InvoiceLineItem> { Data = new List<InvoiceLineItem> { new() { Amount = 5000 } } },
            PeriodEnd = DateTime.UtcNow
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

        // Assert - Verify that the subscription item quantity is always 1 and has Id
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Any(item =>
                    item.Id == "si_premium" &&
                    item.Price == targetPlan.PasswordManager.StripeSeatPlanId &&
                    item.Quantity == 1)));
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
                    new() { Id = "si_password_manager", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1) },
                    new() { Id = "si_storage", Price = new Price { Id = "storage-gb-annually" }, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1) }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } },
            Lines = new StripeList<InvoiceLineItem> { Data = new List<InvoiceLineItem> { new() { Amount = 5000 } } },
            PeriodEnd = DateTime.UtcNow
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

        // Assert - Verify password manager item is modified and storage item is deleted
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                // Password manager item should be modified to new plan price, not deleted
                options.SubscriptionDetails.Items.Any(item =>
                    item.Id == "si_password_manager" &&
                    item.Price == targetPlan.PasswordManager.StripeSeatPlanId &&
                    item.Deleted != true) &&
                // Storage item should be deleted
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
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1) }
                }
            }
        };

        var targetPlan = new FamiliesPlan(); // families is non seat based

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } },
            Lines = new StripeList<InvoiceLineItem> { Data = new List<InvoiceLineItem> { new() { Amount = 5000 } } },
            PeriodEnd = DateTime.UtcNow
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, PlanType.FamiliesAnnually, billingAddress);

        // Assert - Verify non-seat-based plan uses StripePlanId with quantity 1 and modifies existing item
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Any(item =>
                    item.Id == "si_premium" &&
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
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1) }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } },
            Lines = new StripeList<InvoiceLineItem> { Data = new List<InvoiceLineItem> { new() { Amount = 5000 } } },
            PeriodEnd = DateTime.UtcNow
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

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
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1) }
                }
            }
        };

        // Use Teams which is seat-based
        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } },
            Lines = new StripeList<InvoiceLineItem> { Data = new List<InvoiceLineItem> { new() { Amount = 5000 } } },
            PeriodEnd = DateTime.UtcNow
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

        // Assert - Verify seat-based plan uses StripeSeatPlanId with quantity 1 and modifies existing item
        await _stripeAdapter.Received(1).CreateInvoicePreviewAsync(
            Arg.Is<InvoiceCreatePreviewOptions>(options =>
                options.SubscriptionDetails.Items.Any(item =>
                    item.Id == "si_premium" &&
                    item.Price == targetPlan.PasswordManager.StripeSeatPlanId &&
                    item.Quantity == 1)));
    }

    [Theory]
    [InlineData(0, 1)]     // Less than 15 days, minimum 1 month
    [InlineData(1, 1)]     // 1 day = 1 month minimum
    [InlineData(14, 1)]    // 14 days = 1 month minimum
    [InlineData(15, 1)]    // 15 days rounds to 1 month
    [InlineData(30, 1)]    // 30 days = 1 month
    [InlineData(44, 1)]    // 44 days rounds to 1 month
    [InlineData(45, 2)]    // 45 days rounds to 2 months
    [InlineData(60, 2)]    // 60 days = 2 months
    [InlineData(90, 3)]    // 90 days = 3 months
    [InlineData(180, 6)]   // 180 days = 6 months
    [InlineData(365, 12)]  // 365 days rounds to 12 months
    public async Task Run_ValidUpgrade_CalculatesNewPlanProratedMonthsCorrectly(int daysRemaining, int expectedMonths)
    {
        // Arrange
        var user = new User
        {
            Premium = true,
            GatewaySubscriptionId = "sub_123",
            GatewayCustomerId = "cus_123"
        };
        var billingAddress = new Core.Billing.Payment.Models.BillingAddress
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

        // Use fixed time to avoid DateTime.UtcNow differences
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentPeriodEnd = now.AddDays(daysRemaining);
        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = currentPeriodEnd }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        var invoice = new Invoice
        {
            Total = 5000,
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 500 } },
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = new List<InvoiceLineItem> { new() { Amount = 5000 } }
            },
            PeriodEnd = now
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        var proration = result.AsT0;
        Assert.Equal(expectedMonths, proration.NewPlanProratedMonths);
    }

    [Theory, BitAutoData]
    public async Task Run_ValidUpgrade_ReturnsNewPlanProratedAmountCorrectly(User user, BillingAddress billingAddress)
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

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentPeriodEnd = now.AddMonths(3);
        var currentSubscription = new Subscription
        {
            Id = "sub_123",
            Customer = new Customer { Id = "cus_123", Discount = null },
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new() { Id = "si_premium", Price = new Price { Id = "premium-annually" }, CurrentPeriodEnd = currentPeriodEnd }
                }
            }
        };

        var targetPlan = new TeamsPlan(isAnnual: true);

        // Invoice showing new plan cost, credit, and net
        var invoice = new Invoice
        {
            Total = 4500, // $45.00 net after $5 credit
            TotalTaxes = new List<InvoiceTotalTax> { new() { Amount = 450 } }, // $4.50
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = new List<InvoiceLineItem>
                {
                    new() { Amount = -500 },  // -$5.00 credit
                    new() { Amount = 5000 }   // $50.00 for new plan
                }
            },
            PeriodEnd = now
        };

        _pricingClient.ListPremiumPlans().Returns(premiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>())
            .Returns(currentSubscription);
        _stripeAdapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(invoice);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        var proration = result.AsT0;

        Assert.Equal(50.00m, proration.NewPlanProratedAmount);
        Assert.Equal(5.00m, proration.Credit);
        Assert.Equal(4.50m, proration.Tax);
        Assert.Equal(45.00m, proration.Total);
    }
}

