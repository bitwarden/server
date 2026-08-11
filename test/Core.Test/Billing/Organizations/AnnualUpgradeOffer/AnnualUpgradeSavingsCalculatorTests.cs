using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
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
        Items = new StripeList<SubscriptionItem> { Data = [.. items] }
    };

    private static Discount Discount(
        string couponId,
        string duration = CouponDurations.Forever,
        decimal? percentOff = 25m,
        string? currency = "usd") => new()
        {
            Id = $"di_{couponId}",
            Source = new DiscountSource
            {
                CouponId = couponId,
                Coupon = new Coupon
                {
                    Id = couponId,
                    Duration = duration,
                    PercentOff = percentOff,
                    Currency = currency
                }
            }
        };

    private static IReadOnlyList<AnnualUpgradeLine> Lines(Subscription subscription, string targetPriceId) =>
        [.. subscription.Items.Data.Select(item => new AnnualUpgradeLine(item, targetPriceId))];

    private static AnnualUpgradePreviewRequests Build(
        Subscription subscription, IReadOnlyList<AnnualUpgradeLine> lines) =>
        AnnualUpgradeSavingsCalculator.BuildPreviewRequests(subscription, lines);

    [Fact]
    public void Build_BothSidesCarryTheSameQuantitiesAndDifferOnlyInPriceIds()
    {
        var monthlySeat = _currentPlan.PasswordManager.StripeSeatPlanId;
        var monthlySmSeat = _currentPlan.SecretsManager.StripeSeatPlanId;
        var seatItem = Item(monthlySeat, 20);
        var smSeatItem = Item(monthlySmSeat, 3);
        var subscription = Subscription(seatItem, smSeatItem);
        AnnualUpgradeLine[] lines =
        [
            new AnnualUpgradeLine(seatItem, _annualLatestPlan.PasswordManager.StripeSeatPlanId),
            new AnnualUpgradeLine(smSeatItem, _annualLatestPlan.SecretsManager.StripeSeatPlanId)
        ];

        var requests = Build(subscription, lines);

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
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        foreach (var options in new[] { requests.Monthly, requests.Annual })
        {
            Assert.Equal("cus_123", options.Customer);
            Assert.False(options.AutomaticTax.Enabled);
            Assert.Equal(BillingMode.Classic, options.SubscriptionDetails.BillingMode.Type);
            // No Subscription set: the preview prices a fresh full term rather than the remainder
            // of the current period, and nothing prorates against an existing schedule.
            Assert.Null(options.Subscription);
        }
    }

    [Fact]
    public void Build_SubscriptionCoupons_PassedAtInvoiceLevelOnBothSides()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("sub_coupon")];

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        Assert.Equal(new[] { "sub_coupon" }, requests.Monthly.Discounts.Select(discount => discount.Coupon));
        Assert.Equal(new[] { "sub_coupon" }, requests.Annual.Discounts.Select(discount => discount.Coupon));
    }

    [Fact]
    public void Build_ForeverCouponWithNullCurrency_IsPassed()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("no_currency", currency: null)];

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        Assert.Equal(new[] { "no_currency" }, (requests.Monthly.Discounts ?? []).Select(d => d.Coupon));
    }

    [Fact]
    public void Build_CustomerCouponUsedOnlyWhenTheSubscriptionHasNoneOfItsOwn()
    {
        var withOwn = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        withOwn.Discounts = [Discount("sub_coupon")];
        withOwn.Customer = new Customer { Id = "cus_123", Discount = Discount("cus_coupon") };

        Assert.Equal(
            new[] { "sub_coupon" },
            Build(withOwn, Lines(withOwn, _annualLatestPlan.PasswordManager.StripeSeatPlanId))
                .Monthly.Discounts.Select(d => d.Coupon));

        var withoutOwn = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        withoutOwn.Customer = new Customer { Id = "cus_123", Discount = Discount("cus_coupon") };

        Assert.Equal(
            new[] { "cus_coupon" },
            Build(withoutOwn, Lines(withoutOwn, _annualLatestPlan.PasswordManager.StripeSeatPlanId))
                .Monthly.Discounts.Select(d => d.Coupon));
    }

    [Theory]
    [InlineData(CouponDurations.Once)]
    [InlineData(CouponDurations.Repeating)]
    public void Build_NonForeverInvoiceCoupon_IsSuppressedNotInherited(string duration)
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("temporary", duration: duration)];

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        // The empty list is the assertion; null would inherit instead of suppressing.
        Assert.Empty(requests.Monthly.Discounts);
        Assert.Empty(requests.Annual.Discounts);
    }

    [Theory]
    [InlineData(CouponDurations.Once)]
    [InlineData(CouponDurations.Repeating)]
    public void Build_NonForeverCustomerCoupon_IsSuppressedNotInherited(string duration)
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Customer = new Customer
        {
            Id = "cus_123",
            Discount = Discount("temporary", duration: duration)
        };

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        Assert.Empty(requests.Monthly.Discounts);
        Assert.Empty(requests.Annual.Discounts);
    }

    [Fact]
    public void Build_ItemCoupon_AttachedToTheMatchingItemNotTheInvoice()
    {
        var monthlySeat = _currentPlan.PasswordManager.StripeSeatPlanId;
        var monthlySmSeat = _currentPlan.SecretsManager.StripeSeatPlanId;
        var seatItem = Item(monthlySeat, 5, Discount("seat_only"));
        var smSeatItem = Item(monthlySmSeat, 3);
        var subscription = Subscription(seatItem, smSeatItem);
        AnnualUpgradeLine[] lines =
        [
            new AnnualUpgradeLine(seatItem, _annualLatestPlan.PasswordManager.StripeSeatPlanId),
            new AnnualUpgradeLine(smSeatItem, _annualLatestPlan.SecretsManager.StripeSeatPlanId)
        ];

        var requests = Build(subscription, lines);

        Assert.Empty(requests.Monthly.Discounts);
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

        Assert.Null(Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId))
            .Monthly.SubscriptionDetails.Items[0].Discounts);
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

    [Fact]
    public void Build_CustomerAndSubscriptionCoupons_BothSidesCarrySubscriptionsOwnNotTheCustomers()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("sub_coupon")];
        subscription.Customer = new Customer { Id = "cus_123", Discount = Discount("cus_coupon") };

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        // The annual phase carries the subscription's own discount by reuse and does not activate the
        // dormant customer coupon, so the quote must not add it to the annual side either.
        Assert.Equal(new[] { "sub_coupon" }, requests.Annual.Discounts.Select(discount => discount.Coupon));
        Assert.Equal(new[] { "sub_coupon" }, requests.Monthly.Discounts.Select(discount => discount.Coupon));
    }

    [Fact]
    public void Build_CustomerCouponOnly_BothSidesCarryIt()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Customer = new Customer { Id = "cus_123", Discount = Discount("cus_coupon") };

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        Assert.Equal(new[] { "cus_coupon" }, requests.Monthly.Discounts.Select(d => d.Coupon));
        Assert.Equal(new[] { "cus_coupon" }, requests.Annual.Discounts.Select(d => d.Coupon));
    }

    [Theory]
    [InlineData(CouponDurations.Once)]
    [InlineData(CouponDurations.Repeating)]
    public void Build_NonForeverCustomerCoupon_IsExcludedFromTheAnnualSet(string duration)
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("sub_coupon")];
        subscription.Customer = new Customer
        {
            Id = "cus_123",
            Discount = Discount("temporary", duration: duration)
        };

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        Assert.Equal(new[] { "sub_coupon" }, requests.Annual.Discounts.Select(d => d.Coupon));
        Assert.Equal(new[] { "sub_coupon" }, requests.Monthly.Discounts.Select(d => d.Coupon));
    }

    [Fact]
    public void Build_SameCouponAtBothLevels_AnnualListsItOnce()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));
        subscription.Discounts = [Discount("shared")];
        subscription.Customer = new Customer { Id = "cus_123", Discount = Discount("shared") };

        var requests = Build(subscription, Lines(subscription, _annualLatestPlan.PasswordManager.StripeSeatPlanId));

        Assert.Equal(new[] { "shared" }, requests.Annual.Discounts.Select(d => d.Coupon));
    }
}
