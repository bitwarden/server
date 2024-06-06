using Bit.Core.Billing.Models;
using Bit.Core.Utilities;

namespace Bit.Api.Billing.Models.Responses;

public record ConsolidatedBillingSubscriptionResponse(
    string Status,
    DateTime CurrentPeriodEndDate,
    decimal? DiscountPercentage,
    IEnumerable<ProviderPlanResponse> Plans)
{
    private const string _annualCadence = "Annual";
    private const string _monthlyCadence = "Monthly";

    public static ConsolidatedBillingSubscriptionResponse From(
        ConsolidatedBillingSubscriptionDTO consolidatedBillingSubscription)
    {
        var (providerPlans, subscription) = consolidatedBillingSubscription;

        var providerPlansDTO = providerPlans
            .Select(providerPlan =>
            {
                var plan = StaticStore.GetPlan(providerPlan.PlanType);
                var cost = (providerPlan.SeatMinimum + providerPlan.PurchasedSeats) * plan.PasswordManager.SeatPrice;
                var cadence = plan.IsAnnual ? _annualCadence : _monthlyCadence;
                return new ProviderPlanResponse(
                    plan.Name,
                    providerPlan.SeatMinimum,
                    providerPlan.PurchasedSeats,
                    providerPlan.AssignedSeats,
                    cost,
                    cadence);
            });

        return new ConsolidatedBillingSubscriptionResponse(
            subscription.Status,
            subscription.CurrentPeriodEnd,
            subscription.Customer?.Discount?.Coupon?.PercentOff,
            providerPlansDTO);
    }
}

public record ProviderPlanResponse(
    string PlanName,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats,
    decimal Cost,
    string Cadence);
