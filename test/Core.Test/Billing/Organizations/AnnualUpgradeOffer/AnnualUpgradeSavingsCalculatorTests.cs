using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Test.Billing.Mocks.Plans;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer;

public class AnnualUpgradeSavingsCalculatorTests
{
    private readonly Teams2020Plan _currentPlan = new(isAnnual: false);
    private readonly TeamsPlan _annualLatestPlan = new(isAnnual: true);

    private static SubscriptionItem Item(string priceId, long quantity, string productId = "prod_pm") => new()
    {
        Id = $"si_{priceId}",
        Quantity = quantity,
        Price = new Price { Id = priceId, ProductId = productId }
    };

    private static Subscription Subscription(params SubscriptionItem[] items) => new()
    {
        Id = "sub_123",
        Currency = "usd",
        Items = new StripeList<SubscriptionItem> { Data = [.. items] }
    };

    [Fact]
    public void Calculate_SeatsOnly_MultipliesMonthlyByTwelve()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5));

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.NotNull(result);
        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 5 * 12, result!.Value.CurrentAnnualCost);
        Assert.Equal(_annualLatestPlan.PasswordManager.SeatPrice * 5, result.Value.NewAnnualCost);
    }

    [Fact]
    public void Calculate_IncludesSecretsManagerSeatsAndServiceAccounts()
    {
        var subscription = Subscription(
            Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            Item(_currentPlan.SecretsManager.StripeSeatPlanId, 3, "prod_sm"),
            Item(_currentPlan.SecretsManager.StripeServiceAccountPlanId, 2, "prod_sa"));

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        var expectedMonthly =
            _currentPlan.PasswordManager.SeatPrice * 5 +
            _currentPlan.SecretsManager.SeatPrice * 3 +
            _currentPlan.SecretsManager.AdditionalPricePerServiceAccount!.Value * 2;
        var expectedAnnual =
            _annualLatestPlan.PasswordManager.SeatPrice * 5 +
            _annualLatestPlan.SecretsManager.SeatPrice * 3 +
            _annualLatestPlan.SecretsManager.AdditionalPricePerServiceAccount!.Value * 2;

        Assert.NotNull(result);
        Assert.Equal(expectedMonthly * 12, result!.Value.CurrentAnnualCost);
        Assert.Equal(expectedAnnual, result.Value.NewAnnualCost);
    }

    [Fact]
    public void Calculate_IncludesAdditionalStorage()
    {
        var subscription = Subscription(
            Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            Item(_currentPlan.PasswordManager.StripeStoragePlanId, 4, "prod_storage"));

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        var expectedMonthly =
            _currentPlan.PasswordManager.SeatPrice * 5 +
            _currentPlan.PasswordManager.AdditionalStoragePricePerGb * 4;
        var expectedAnnual =
            _annualLatestPlan.PasswordManager.SeatPrice * 5 +
            _annualLatestPlan.PasswordManager.AdditionalStoragePricePerGb * 4;

        Assert.NotNull(result);
        Assert.Equal(expectedMonthly * 12, result!.Value.CurrentAnnualCost);
        Assert.Equal(expectedAnnual, result.Value.NewAnnualCost);
    }

    [Fact]
    public void Calculate_UnmappableLineItem_ReturnsNull()
    {
        var subscription = Subscription(
            Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5),
            Item("price_sponsorship", 1));

        Assert.Null(AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan));
    }

    [Fact]
    public void Calculate_NoLineItems_ReturnsNull()
    {
        Assert.Null(AnnualUpgradeSavingsCalculator.Calculate(Subscription(), _currentPlan, _annualLatestPlan));
    }

    [Fact]
    public void Calculate_ItemWithNullPrice_IsSkippedNotTreatedAsUnmappable()
    {
        var priceless = new SubscriptionItem { Id = "si_null", Quantity = 1, Price = null };
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 5), priceless);

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.NotNull(result);
        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 5 * 12, result!.Value.CurrentAnnualCost);
    }

    private static Discount Discount(
        string couponId,
        decimal? percentOff = null,
        long? amountOff = null,
        string duration = "forever",
        string currency = "usd",
        List<string>? appliesToProducts = null) => new()
        {
            Id = $"di_{couponId}",
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = percentOff,
                AmountOff = amountOff,
                Duration = duration,
                Currency = currency,
                AppliesTo = appliesToProducts is null ? null : new CouponAppliesTo { Products = appliesToProducts }
            }
        };

    [Fact]
    public void Calculate_ItemLevelPercentOff_ReducesThatLineOnly()
    {
        var seats = Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10);
        seats.Discounts = [Discount("half", percentOff: 50m)];
        var storage = Item(_currentPlan.PasswordManager.StripeStoragePlanId, 2, "prod_storage");
        var subscription = Subscription(seats, storage);

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        var expectedMonthly =
            _currentPlan.PasswordManager.SeatPrice * 10 * 0.5m +
            _currentPlan.PasswordManager.AdditionalStoragePricePerGb * 2;
        Assert.Equal(expectedMonthly * 12, result!.Value.CurrentAnnualCost);
    }

    [Fact]
    public void Calculate_ItemLevelAmountOff_AppliesPerInvoiceAndClampsAtZero()
    {
        var seats = Item(_currentPlan.PasswordManager.StripeSeatPlanId, 1);
        // 10,000.00 off a single seat line: the line floors at zero rather than going negative.
        seats.Discounts = [Discount("huge", amountOff: 1_000_000)];
        var subscription = Subscription(seats);

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(0m, result!.Value.CurrentAnnualCost);
        Assert.Equal(0m, result.Value.NewAnnualCost);
    }

    [Fact]
    public void Calculate_SubscriptionPercentOff_AppliesToWholeSubtotalOnBothSides()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts = [Discount("tenpct", percentOff: 10m)];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 10 * 0.9m * 12, result!.Value.CurrentAnnualCost);
        Assert.Equal(_annualLatestPlan.PasswordManager.SeatPrice * 10 * 0.9m, result.Value.NewAnnualCost);
    }

    [Fact]
    public void Calculate_SubscriptionAmountOff_DeductsTwelveTimesMonthlyAndOnceAnnually()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 100));
        subscription.Discounts = [Discount("fivedollars", amountOff: 500)];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal((_currentPlan.PasswordManager.SeatPrice * 100 - 5m) * 12, result!.Value.CurrentAnnualCost);
        Assert.Equal(_annualLatestPlan.PasswordManager.SeatPrice * 100 - 5m, result.Value.NewAnnualCost);
    }

    [Fact]
    public void Calculate_CustomerDiscountIgnoredWhenSubscriptionHasItsOwn_OnBothSides()
    {
        // Stripe gates customer inheritance on the subscription having no discounts of its own, so
        // the two never stack. The redemption is discount-neutral, so the annual side sees the same
        // coupon the monthly side does and the customer coupon is ignored throughout.
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts = [Discount("subpct", percentOff: 10m)];
        subscription.Customer = new Customer { Discount = Discount("custpct", percentOff: 50m) };

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 10 * 0.9m * 12, result!.Value.CurrentAnnualCost);
        Assert.Equal(_annualLatestPlan.PasswordManager.SeatPrice * 10 * 0.9m, result.Value.NewAnnualCost);
    }

    [Fact]
    public void Calculate_CustomerDiscountAppliesWhenSubscriptionHasNone_OnBothSides()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10));
        subscription.Customer = new Customer { Discount = Discount("custpct", percentOff: 20m) };

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 10 * 0.8m * 12, result!.Value.CurrentAnnualCost);
        Assert.Equal(_annualLatestPlan.PasswordManager.SeatPrice * 10 * 0.8m, result.Value.NewAnnualCost);
    }

    [Theory]
    [InlineData("once")]
    [InlineData("repeating")]
    public void Calculate_NonForeverDuration_Ignored(string duration)
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts = [Discount("temporary", percentOff: 50m, duration: duration)];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 10 * 12, result!.Value.CurrentAnnualCost);
    }

    [Fact]
    public void Calculate_ProductScopedCoupon_AppliesOnlyToMatchingLines()
    {
        var subscription = Subscription(
            Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10),
            Item(_currentPlan.SecretsManager.StripeSeatPlanId, 4, "prod_sm"));
        subscription.Discounts = [Discount("smonly", percentOff: 50m, appliesToProducts: ["prod_sm"])];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        var expectedMonthly =
            _currentPlan.PasswordManager.SeatPrice * 10 +
            _currentPlan.SecretsManager.SeatPrice * 4 * 0.5m;
        Assert.Equal(expectedMonthly * 12, result!.Value.CurrentAnnualCost);
    }

    [Fact]
    public void Calculate_ProductScopedCouponMatchingNoLine_IsSkipped()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts = [Discount("nomatch", amountOff: 500, appliesToProducts: ["prod_absent"])];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 10 * 12, result!.Value.CurrentAnnualCost);
    }

    [Fact]
    public void Calculate_ItemDiscountAppliedBeforeSubscriptionDiscount()
    {
        var seats = Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10);
        seats.Discounts = [Discount("item50", percentOff: 50m)];
        var subscription = Subscription(seats);
        subscription.Discounts = [Discount("sub10", percentOff: 10m)];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 10 * 0.5m * 0.9m * 12, result!.Value.CurrentAnnualCost);
    }

    [Fact]
    public void Calculate_ScopedCouponThenUnscopedCoupon_SecondSeesReducedAmounts()
    {
        var subscription = Subscription(
            Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10),
            Item(_currentPlan.SecretsManager.StripeSeatPlanId, 4, "prod_sm"));
        subscription.Discounts =
        [
            Discount("smhalf", percentOff: 50m, appliesToProducts: ["prod_sm"]),
            Discount("alltenpct", percentOff: 10m)
        ];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        var afterScoped =
            _currentPlan.PasswordManager.SeatPrice * 10 +
            _currentPlan.SecretsManager.SeatPrice * 4 * 0.5m;
        Assert.Equal(afterScoped * 0.9m * 12, result!.Value.CurrentAnnualCost);
    }

    [Fact]
    public void Calculate_CouponCurrencyMismatch_Ignored()
    {
        var subscription = Subscription(Item(_currentPlan.PasswordManager.StripeSeatPlanId, 10));
        subscription.Discounts = [Discount("eurodiscount", amountOff: 500, currency: "eur")];

        var result = AnnualUpgradeSavingsCalculator.Calculate(subscription, _currentPlan, _annualLatestPlan);

        Assert.Equal(_currentPlan.PasswordManager.SeatPrice * 10 * 12, result!.Value.CurrentAnnualCost);
    }
}
