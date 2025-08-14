using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Providers.Models;
using Bit.Core.Billing.Tax.Models;
using Stripe;

namespace Bit.Api.Billing.Models.Responses;

public record ProviderSubscriptionResponse(
    string Status,
    DateTime? CurrentPeriodEndDate,
    decimal? DiscountPercentage,
    string CollectionMethod,
    IEnumerable<ProviderPlanResponse> Plans,
    decimal AccountCredit,
    TaxInformation TaxInformation,
    DateTime? CancelAt,
    SubscriptionSuspension Suspension,
    ProviderType ProviderType,
    PaymentSource PaymentSource)
{
    private const string _annualCadence = "Annual";
    private const string _monthlyCadence = "Monthly";

    public static ProviderSubscriptionResponse From(
        Subscription subscription,
        ICollection<ConfiguredProviderPlan> providerPlans,
        TaxInformation taxInformation,
        SubscriptionSuspension subscriptionSuspension,
        Provider provider,
        PaymentSource paymentSource)
    {
        var providerPlanResponses = providerPlans
            .Select(providerPlan =>
            {
                var plan = providerPlan.Plan;
                var cost = (providerPlan.SeatMinimum + providerPlan.PurchasedSeats) * providerPlan.Price;
                var cadence = plan.IsAnnual ? _annualCadence : _monthlyCadence;
                return new ProviderPlanResponse(
                    plan.Name,
                    plan.Type,
                    plan.ProductTier,
                    providerPlan.SeatMinimum,
                    providerPlan.PurchasedSeats,
                    providerPlan.AssignedSeats,
                    cost,
                    cadence);
            });

        var accountCredit = Convert.ToDecimal(subscription.Customer?.Balance) * -1 / 100;

        return new ProviderSubscriptionResponse(
            subscription.Status,
            subscription.GetCurrentPeriodEnd(),
            subscription.Customer?.Discount?.Coupon?.PercentOff,
            subscription.CollectionMethod,
            providerPlanResponses,
            accountCredit,
            taxInformation,
            subscription.CancelAt,
            subscriptionSuspension,
            provider.Type,
            paymentSource);
    }
}

public record ProviderPlanResponse(
    string PlanName,
    PlanType Type,
    ProductTierType ProductTier,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats,
    decimal Cost,
    string Cadence);
