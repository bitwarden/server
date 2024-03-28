using Bit.Core.Billing.Models;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Api.Billing.Models;

public record ProviderSubscriptionDTO(
    string Status,
    DateTime CurrentPeriodEndDate,
    decimal? DiscountPercentage,
    IEnumerable<ProviderPlanDTO> Plans)
{
    private const string _annualCadence = "Annual";
    private const string _monthlyCadence = "Monthly";

    public static ProviderSubscriptionDTO From(
        IEnumerable<ConfiguredProviderPlan> providerPlans,
        Subscription subscription)
    {
        var providerPlansDTO = providerPlans
            .Select(providerPlan =>
            {
                var plan = StaticStore.GetPlan(providerPlan.PlanType);
                var cost = (providerPlan.SeatMinimum + providerPlan.PurchasedSeats) * plan.PasswordManager.SeatPrice;
                var cadence = plan.IsAnnual ? _annualCadence : _monthlyCadence;
                return new ProviderPlanDTO(
                    plan.Name,
                    providerPlan.SeatMinimum,
                    providerPlan.PurchasedSeats,
                    cost,
                    cadence);
            });

        return new ProviderSubscriptionDTO(
            subscription.Status,
            subscription.CurrentPeriodEnd,
            subscription.Customer?.Discount?.Coupon?.PercentOff,
            providerPlansDTO);
    }
}

public record ProviderPlanDTO(
    string PlanName,
    int SeatMinimum,
    int PurchasedSeats,
    decimal Cost,
    string Cadence);
