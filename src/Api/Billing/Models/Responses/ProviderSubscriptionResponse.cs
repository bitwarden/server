using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Api.Billing.Models.Responses;

public record ProviderSubscriptionResponse(
    string Status,
    DateTime CurrentPeriodEndDate,
    decimal? DiscountPercentage,
    string CollectionMethod,
    IEnumerable<ProviderPlanResponse> Plans,
    decimal AccountCredit,
    TaxInformation TaxInformation,
    DateTime? CancelAt,
    SubscriptionSuspension Suspension)
{
    private const string _annualCadence = "Annual";
    private const string _monthlyCadence = "Monthly";

    public static ProviderSubscriptionResponse From(
        Subscription subscription,
        ICollection<ProviderPlan> providerPlans,
        TaxInformation taxInformation,
        SubscriptionSuspension subscriptionSuspension)
    {
        var providerPlanResponses = providerPlans
            .Where(providerPlan => providerPlan.IsConfigured())
            .Select(ConfiguredProviderPlan.From)
            .Select(configuredProviderPlan =>
            {
                var plan = StaticStore.GetPlan(configuredProviderPlan.PlanType);
                var cost = (configuredProviderPlan.SeatMinimum + configuredProviderPlan.PurchasedSeats) * plan.PasswordManager.ProviderPortalSeatPrice;
                var cadence = plan.IsAnnual ? _annualCadence : _monthlyCadence;
                return new ProviderPlanResponse(
                    plan.Name,
                    configuredProviderPlan.SeatMinimum,
                    configuredProviderPlan.PurchasedSeats,
                    configuredProviderPlan.AssignedSeats,
                    cost,
                    cadence);
            });

        var accountCredit = Convert.ToDecimal(subscription.Customer?.Balance) * -1 / 100;

        return new ProviderSubscriptionResponse(
            subscription.Status,
            subscription.CurrentPeriodEnd,
            subscription.Customer?.Discount?.Coupon?.PercentOff,
            subscription.CollectionMethod,
            providerPlanResponses,
            accountCredit,
            taxInformation,
            subscription.CancelAt,
            subscriptionSuspension);
    }
}

public record ProviderPlanResponse(
    string PlanName,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats,
    decimal Cost,
    string Cadence);
