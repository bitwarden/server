using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Billing.Pricing;

public interface IPricingClient
{
    /// <summary>
    /// Retrieve a Bitwarden plan by its <paramref name="planType"/>. If the feature flag 'use-pricing-service' is enabled,
    /// this will trigger a request to the Bitwarden Pricing Service. Otherwise, it will use the existing <see cref="StaticStore"/>.
    /// </summary>
    /// <param name="planType">The type of plan to retrieve.</param>
    /// <returns>A Bitwarden <see cref="Plan"/> record.</returns>
    /// <exception cref="BillingException">Thrown when the method is invoked by a Self-Hosted instance.</exception>
    /// <exception cref="NotFoundException">Thrown when <see cref="Plan"/> related to the provided <paramref name="planType"/> couldn't be found.</exception>
    /// <exception cref="BillingException">Thrown when the request to the Pricing Service fails unexpectedly.</exception>
    Task<Plan> GetPlan(PlanType planType);

    /// <summary>
    /// Retrieve all of the Bitwarden plans. If the feature flag 'use-pricing-service' is enabled,
    /// this will trigger a request to the Bitwarden Pricing Service. Otherwise, it will use the existing <see cref="StaticStore"/>.
    /// </summary>
    /// <returns>A list of Bitwarden <see cref="Plan"/> records.</returns>
    /// <exception cref="BillingException">Thrown when the method is invoked by a Self-Hosted instance.</exception>
    /// <exception cref="BillingException">Thrown when the request to the Pricing Service fails unexpectedly.</exception>
    Task<List<Plan>> ListPlans();
}
