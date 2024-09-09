using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

public interface IOrganizationBillingService
{
    /// <summary>
    /// Retrieve metadata about the organization represented bsy the provided <paramref name="organizationId"/>.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to retrieve metadata for.</param>
    /// <returns>An <see cref="OrganizationMetadata"/> record.</returns>
    Task<OrganizationMetadata> GetMetadata(Guid organizationId);

    /// <summary>
    /// Purchase a subscription for the provided <paramref name="organization"/> using the provided <paramref name="organizationSubscriptionPurchase"/>.
    /// If successful, a Stripe <see cref="Stripe.Customer"/> and <see cref="Stripe.Subscription"/> will be created for the organization and the
    /// organization will be enabled.
    /// </summary>
    /// <param name="organization">The organization to purchase a subscription for.</param>
    /// <param name="organizationSubscriptionPurchase">The purchase information for the organization's subscription.</param>
    Task PurchaseSubscription(Organization organization, OrganizationSubscriptionPurchase organizationSubscriptionPurchase);
}
