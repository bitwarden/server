using Bit.Core.Billing.Models;
using Bit.Core.Utilities;

namespace Bit.Api.Billing.Models.Responses;

public record ConsolidatedBillingSubscriptionResponse(
    string Status,
    DateTime CurrentPeriodEndDate,
    decimal? DiscountPercentage,
    string CollectionMethod,
    IEnumerable<ProviderPlanResponse> Plans,
    long AccountCredit,
    TaxInformationDTO TaxInformation,
    DateTime? CancelAt,
    SubscriptionSuspensionDTO Suspension)
{
    private const string _annualCadence = "Annual";
    private const string _monthlyCadence = "Monthly";

    public static ConsolidatedBillingSubscriptionResponse From(
        ConsolidatedBillingSubscriptionDTO consolidatedBillingSubscription)
    {
        var (providerPlans, subscription, taxInformation, suspension) = consolidatedBillingSubscription;

        var providerPlanResponses = providerPlans
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

        return new ConsolidatedBillingSubscriptionResponse(
            subscription.Status,
            subscription.CurrentPeriodEnd,
            subscription.Customer?.Discount?.Coupon?.PercentOff,
            subscription.CollectionMethod,
            providerPlanResponses,
            subscription.Customer?.Balance ?? 0,
            taxInformation,
            subscription.CancelAt,
            suspension);
    }
}

public record ProviderPlanResponse(
    string PlanName,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats,
    decimal Cost,
    string Cadence);
