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

        Assert.NotNull(result);
        Assert.Equal(expectedMonthly * 12, result!.Value.CurrentAnnualCost);
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
}
