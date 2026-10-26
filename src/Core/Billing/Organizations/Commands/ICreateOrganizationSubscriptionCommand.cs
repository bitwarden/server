using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Models;
using Stripe;

namespace Bit.Core.Billing.Organizations.Commands;

/// <summary>
/// Establishes the organization's Stripe customer and subscription for a net-new purchase, stages the result
/// onto the organization (gateway ids, Enabled/ExpirationDate, Privileged Controls fields) for the caller to
/// persist, and returns the subscription.
/// </summary>
/// <remarks>
/// This interface is owned by Core so that consumers which cannot reference the Subscriptions.Organization library
/// (such as the cloud organization sign-up command) can depend on it; the implementation lives in that library.
/// </remarks>
public interface ICreateOrganizationSubscriptionCommand
{
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">Thrown when the purchase is invalid (e.g. bad payment/tax, expired discount).</exception>
    Task<Subscription> Run(Organization organization, OrganizationSubscriptionCreation purchase);
}
