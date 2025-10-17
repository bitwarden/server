using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Pricing;

using OrganizationPlan = Plan;
using PremiumPlan = Premium.Plan;

public interface IPricingClient
{
    // TODO: Rename with Organization focus.
    /// <summary>
    /// Retrieve a Bitwarden plan by its <paramref name="planType"/>. If the feature flag 'use-pricing-service' is enabled,
    /// this will trigger a request to the Bitwarden Pricing Service. Otherwise, it will use the existing <see cref="StaticStore"/>.
    /// </summary>
    /// <param name="planType">The type of plan to retrieve.</param>
    /// <returns>A Bitwarden <see cref="Plan"/> record or null in the case the plan could not be found or the method was executed from a self-hosted instance.</returns>
    /// <exception cref="BillingException">Thrown when the request to the Pricing Service fails unexpectedly.</exception>
    Task<OrganizationPlan?> GetPlan(PlanType planType);

    // TODO: Rename with Organization focus.
    /// <summary>
    /// Retrieve a Bitwarden plan by its <paramref name="planType"/>. If the feature flag 'use-pricing-service' is enabled,
    /// this will trigger a request to the Bitwarden Pricing Service. Otherwise, it will use the existing <see cref="StaticStore"/>.
    /// </summary>
    /// <param name="planType">The type of plan to retrieve.</param>
    /// <returns>A Bitwarden <see cref="Plan"/> record.</returns>
    /// <exception cref="NotFoundException">Thrown when the <see cref="Plan"/> for the provided <paramref name="planType"/> could not be found or the method was executed from a self-hosted instance.</exception>
    /// <exception cref="BillingException">Thrown when the request to the Pricing Service fails unexpectedly.</exception>
    Task<OrganizationPlan> GetPlanOrThrow(PlanType planType);

    // TODO: Rename with Organization focus.
    /// <summary>
    /// Retrieve all the Bitwarden plans. If the feature flag 'use-pricing-service' is enabled,
    /// this will trigger a request to the Bitwarden Pricing Service. Otherwise, it will use the existing <see cref="StaticStore"/>.
    /// </summary>
    /// <returns>A list of Bitwarden <see cref="Plan"/> records or an empty list in the case the method is executed from a self-hosted instance.</returns>
    /// <exception cref="BillingException">Thrown when the request to the Pricing Service fails unexpectedly.</exception>
    Task<List<OrganizationPlan>> ListPlans();

    Task<PremiumPlan> GetAvailablePremiumPlan();
    Task<List<PremiumPlan>> ListPremiumPlans();
}
