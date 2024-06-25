using Bit.Core.Billing.Models;
using Bit.Core.Utilities;

namespace Bit.Api.Billing.Models.Responses;

public record ConsolidatedBillingSubscriptionResponse(
    string Status,
    DateTime CurrentPeriodEndDate,
    decimal? DiscountPercentage,
    string CollectionMethod,
    DateTime? UnpaidPeriodEndDate,
    int? GracePeriod,
    DateTime? SuspensionDate,
    DateTime? CancelAt,
    IEnumerable<ProviderPlanResponse> Plans)
{
    private const string _annualCadence = "Annual";
    private const string _monthlyCadence = "Monthly";

    public static ConsolidatedBillingSubscriptionResponse From(
        ConsolidatedBillingSubscriptionDTO consolidatedBillingSubscription)
    {
        var (providerPlans, subscription, suspensionDate, unpaidPeriodEndDate) = consolidatedBillingSubscription;

        var providerPlansDTO = providerPlans
            .Select(providerPlan =>
            {
                var plan = StaticStore.GetPlan(providerPlan.PlanType);
                var cost = (providerPlan.SeatMinimum + providerPlan.PurchasedSeats) * plan.PasswordManager.ProviderPortalSeatPrice;
                var cadence = plan.IsAnnual ? _annualCadence : _monthlyCadence;
                return new ProviderPlanResponse(
                    plan.Name,
                    providerPlan.SeatMinimum,
                    providerPlan.PurchasedSeats,
                    providerPlan.AssignedSeats,
                    cost,
                    cadence);
            });

        var gracePeriod = subscription.CollectionMethod == "charge_automatically" ? 14 : 30;

        return new ConsolidatedBillingSubscriptionResponse(
            subscription.Status,
            subscription.CurrentPeriodEnd,
            subscription.Customer?.Discount?.Coupon?.PercentOff,
            subscription.CollectionMethod,
            unpaidPeriodEndDate,
            gracePeriod,
            suspensionDate,
            subscription.CancelAt,
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
