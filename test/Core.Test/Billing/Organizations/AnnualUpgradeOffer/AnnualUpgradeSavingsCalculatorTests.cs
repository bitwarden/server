using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Test.Billing.Mocks.Plans;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer;

using static StripeConstants;

public class AnnualUpgradeSavingsCalculatorTests
{
    private readonly Teams2020Plan _currentPlan = new(isAnnual: false);
    private readonly Teams2020Plan _annualLatestPlan = new(isAnnual: true);

    private static SubscriptionItem Item(
        string priceId, long quantity, params Discount[] discounts) => new()
        {
            Id = $"si_{priceId}",
            Quantity = quantity,
            Price = new Price { Id = priceId, ProductId = $"prod_{priceId}" },
            Discounts = discounts.Length == 0 ? null : [.. discounts]
        };

    private static Subscription Subscription(params SubscriptionItem[] items) => new()
    {
        Id = "sub_123",
        CustomerId = "cus_123",
        Currency = "usd",
        Items = new StripeList<SubscriptionItem> { Data = [.. items] }
    };

    private static Discount Discount(
        string couponId,
        string duration = CouponDurations.Forever,
        decimal? percentOff = 25m,
        long? amountOff = null,
        string? currency = "usd") => new()
        {
            Id = $"di_{couponId}",
            Coupon = new Coupon
            {
                Id = couponId,
                Duration = duration,
                PercentOff = percentOff,
                AmountOff = amountOff,
                Currency = currency
            }
        };

    private AnnualUpgradePreviewRequests Build(Subscription subscription) =>
        AnnualUpgradeSavingsCalculator.BuildPreviewRequestsOrNull(
            subscription, _currentPlan, _annualLatestPlan)
        ?? throw new Xunit.Sdk.XunitException("expected a payload pair, got null");

    [Fact]
    public void Build_BothSidesCarryTheSameQuantitiesAndDifferOnlyInPriceIds()
    {
        var monthlySeat = _currentPlan.PasswordManager.StripeSeatPlanId;
        var monthlySmSeat = _currentPlan.SecretsManager.StripeSeatPlanId;

        var requests = Build(Subscription(Item(monthlySeat, 20), Item(monthlySmSeat, 3)));

        var monthlyItems = requests.Monthly.SubscriptionDetails.Items;
        var annualItems = requests.Annual.SubscriptionDetails.Items;

        Assert.Equal(2, monthlyItems.Count);
        Assert.Equal(2, annualItems.Count);
        Assert.Equal(new[] { 20L, 3L }, monthlyItems.Select(item => item.Quantity!.Value));
        Assert.Equal(new[] { 20L, 3L }, annualItems.Select(item => item.Quantity!.Value));
        Assert.Equal(new[] { monthlySeat, monthlySmSeat }, monthlyItems.Select(item => item.Price));
        Assert.Equal(
            new[]
            {
                _annualLatestPlan.PasswordManager.StripeSeatPlanId,
                _annualLatestPlan.SecretsManager.StripeSeatPlanId
            },
            annualItems.Select(item => item.Price));
    }

    [Fact]
    public void Build_TargetsTheCustomerAndDisablesAutomaticTaxOnBothSides()
    {
        var requests = Build(Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5)));

        foreach (var options in new[] { requests.Monthly, requests.Annual })
        {
            Assert.Equal("cus_123", options.Customer);
            Assert.False(options.AutomaticTax.Enabled);
            // No Subscription set: the preview prices a fresh full term rather than the remainder
            // of the current period, and nothing prorates against an existing schedule.
            Assert.Null(options.Subscription);
        }
    }

    [Fact]
    public void Build_UnmappableLineItem_ReturnsNull()
    {
        Assert.Null(AnnualUpgradeSavingsCalculator.BuildPreviewRequestsOrNull(
            Subscription(
                Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
                Item("price_sponsorship", 1)),
            _currentPlan, _annualLatestPlan));
    }

    [Fact]
    public void Build_NoLineItems_ReturnsNull()
    {
        Assert.Null(AnnualUpgradeSavingsCalculator.BuildPreviewRequestsOrNull(
            Subscription(), _currentPlan, _annualLatestPlan));
    }

    [Fact]
    public void Build_ItemWithNullPrice_IsSkippedNotTreatedAsUnmappable()
    {
        var subscription = Subscription(
            new SubscriptionItem { Id = "si_null", Quantity = 1, Price = null },
            Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));

        var requests = Build(subscription);

        Assert.Single(requests.Monthly.SubscriptionDetails.Items);
    }

    [Fact]
    public void Build_SubscriptionCoupons_PassedAtInvoiceLevelOnBothSides()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("sub_coupon")];

        var requests = Build(subscription);

        Assert.Equal(new[] { "sub_coupon" }, requests.Monthly.Discounts.Select(discount => discount.Coupon));
        Assert.Equal(new[] { "sub_coupon" }, requests.Annual.Discounts.Select(discount => discount.Coupon));
    }

    [Fact]
    public void Build_CustomerCouponUsedOnlyWhenTheSubscriptionHasNoneOfItsOwn()
    {
        var withOwn = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        withOwn.Discounts = [Discount("sub_coupon")];
        withOwn.Customer = new Customer { Id = "cus_123", Discount = Discount("cus_coupon") };

        Assert.Equal(new[] { "sub_coupon" }, Build(withOwn).Monthly.Discounts.Select(d => d.Coupon));

        var withoutOwn = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        withoutOwn.Customer = new Customer { Id = "cus_123", Discount = Discount("cus_coupon") };

        Assert.Equal(new[] { "cus_coupon" }, Build(withoutOwn).Monthly.Discounts.Select(d => d.Coupon));
    }

    [Theory]
    [InlineData(CouponDurations.Once)]
    [InlineData(CouponDurations.Repeating)]
    public void Build_NonForeverInvoiceCoupon_IsNotPassed(string duration)
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("temporary", duration: duration)];

        Assert.Null(Build(subscription).Monthly.Discounts);
    }

    [Fact]
    public void Build_AmountOffCouponInAnotherCurrency_IsNotPassed()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts =
            [Discount("eur_off", percentOff: null, amountOff: 1000, currency: "eur")];

        Assert.Null(Build(subscription).Monthly.Discounts);
    }

    [Fact]
    public void Build_PercentOffCouponWithNullCurrency_IsStillPassed()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("pct", currency: null)];

        Assert.Equal(new[] { "pct" }, Build(subscription).Monthly.Discounts.Select(d => d.Coupon));
    }

    [Fact]
    public void Build_ItemCoupon_AttachedToTheMatchingItemNotTheInvoice()
    {
        var monthlySeat = _currentPlan.PasswordManager.StripeSeatPlanId;
        var monthlySmSeat = _currentPlan.SecretsManager.StripeSeatPlanId;
        var subscription = Subscription(
            Item(monthlySeat, 5, Discount("seat_only")),
            Item(monthlySmSeat, 3));

        var requests = Build(subscription);

        Assert.Null(requests.Monthly.Discounts);
        Assert.Equal(
            new[] { "seat_only" },
            requests.Monthly.SubscriptionDetails.Items[0].Discounts.Select(d => d.Coupon));
        Assert.Null(requests.Monthly.SubscriptionDetails.Items[1].Discounts);
        // The same coupon rides the corresponding annual line. Stripe applies zero if its
        // applies_to excludes the annual product, which is the whole point of asking Stripe.
        Assert.Equal(
            new[] { "seat_only" },
            requests.Annual.SubscriptionDetails.Items[0].Discounts.Select(d => d.Coupon));
    }

    [Theory]
    [InlineData(CouponDurations.Once)]
    [InlineData(CouponDurations.Repeating)]
    public void Build_NonForeverItemCoupon_IsNotPassed(string duration)
    {
        var subscription = Subscription(Item(
            _currentPlan.PasswordManager.StripeSeatPlanId, 5, Discount("temp", duration: duration)));

        Assert.Null(Build(subscription).Monthly.SubscriptionDetails.Items[0].Discounts);
    }

    [Fact]
    public void SavingsFromPreviews_MultipliesTheMonthlyTotalByTwelveAndLeavesTheAnnualAlone()
    {
        // A fixed-amount coupon comes off one invoice regardless of interval, so the comparison
        // has to be built from one monthly invoice and then multiplied, never the reverse.
        var savings = AnnualUpgradeSavingsCalculator.SavingsFromPreviews(
            new Invoice { Total = 40_000 },
            new Invoice { Total = 360_000 });

        Assert.NotNull(savings);
        Assert.Equal(4800m, savings.Value.CurrentAnnualCost);
        Assert.Equal(3600m, savings.Value.NewAnnualCost);
    }

    [Fact]
    public void SavingsFromPreviews_ConvertsFromMinorUnits()
    {
        var savings = AnnualUpgradeSavingsCalculator.SavingsFromPreviews(
            new Invoice { Total = 1_999 },
            new Invoice { Total = 19_999 });

        Assert.NotNull(savings);
        Assert.Equal(239.88m, savings.Value.CurrentAnnualCost);
        Assert.Equal(199.99m, savings.Value.NewAnnualCost);
    }

    [Fact]
    public void SavingsFromPreviews_MissingInvoice_ReturnsNull()
    {
        Assert.Null(AnnualUpgradeSavingsCalculator.SavingsFromPreviews(null, new Invoice { Total = 1 }));
        Assert.Null(AnnualUpgradeSavingsCalculator.SavingsFromPreviews(new Invoice { Total = 1 }, null));
    }
}
