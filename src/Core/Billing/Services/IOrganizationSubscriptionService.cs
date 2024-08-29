using Bit.Core.AdminConsole.Entities;
using StaticStore = Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Services;

public interface IOrganizationSubscriptionService
{
    /// <summary>
    /// Purchases an organization subscription without requiring a payment method at the time of purchase.
    /// </summary>
    /// <param name="subscriber">The subscriber for whom the organization is being purchased.</param>
    /// <param name="plan">The plan chosen for the organization.</param>
    /// <param name="additionalSeats">The number of additional seats to be added to the subscription.</param>
    /// <param name="premiumAccessAddon">Indicates whether the premium access addon should be included in the subscription.</param>
    /// <param name="additionalSmSeats">The number of additional seats for service managers (optional).</param>
    /// <param name="additionalServiceAccount">The number of additional service accounts to be included in the subscription (optional).</param>
    /// <param name="signupIsFromSecretsManagerTrial">Indicates if the signup is from a Secrets Manager trial, allowing for specific trial-related behavior (optional).</param>
    /// <returns>A task that represents the asynchronous operation, containing the purchase status or details.</returns>
    Task<string> PurchaseOrganizationNoPaymentMethod(
        Organization org,
        StaticStore.Plan plan,
        int additionalSeats,
        bool premiumAccessAddon,
        int additionalSmSeats = 0,
        int additionalServiceAccount = 0,
        bool signupIsFromSecretsManagerTrial = false);
}
