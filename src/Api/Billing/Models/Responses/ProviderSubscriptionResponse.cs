using Bit.Core.Billing.Models;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Api.Billing.Models.Responses;

public record ProviderSubscriptionResponse(
    string Status,
    DateTime CurrentPeriodEndDate,
    decimal? DiscountPercentage,
    IEnumerable<ProviderPlanDTO> Plans)
{
    private const string _annualCadence = "Annual";
    private const string _monthlyCadence = "Monthly";

    public static ProviderSubscriptionResponse From(
        IEnumerable<ConfiguredProviderPlanDTO> providerPlans,
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
                    providerPlan.AssignedSeats,
                    cost,
                    cadence);
            });

        return new ProviderSubscriptionResponse(
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
    int AssignedSeats,
    decimal Cost,
    string Cadence);
