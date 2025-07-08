// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Tax.Requests;
using Bit.Core.Billing.Tax.Responses;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Services;

public interface IPaymentService
{
    Task CancelAndRecoverChargesAsync(ISubscriber subscriber);
    Task SponsorOrganizationAsync(Organization org, OrganizationSponsorship sponsorship);
    Task RemoveOrganizationSponsorshipAsync(Organization org, OrganizationSponsorship sponsorship);
    Task<string> AdjustSubscription(
        Organization organization,
        Plan updatedPlan,
        int newlyPurchasedPasswordManagerSeats,
        bool subscribedToSecretsManager,
        int? newlyPurchasedSecretsManagerSeats,
        int? newlyPurchasedAdditionalSecretsManagerServiceAccounts,
        int newlyPurchasedAdditionalStorage);
    Task<string> AdjustSeatsAsync(Organization organization, Plan plan, int additionalSeats);
    Task<string> AdjustSmSeatsAsync(Organization organization, Plan plan, int additionalSeats);
    Task<string> AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage, string storagePlanId);

    Task<string> AdjustServiceAccountsAsync(Organization organization, Plan plan, int additionalServiceAccounts);
    Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false);
    Task ReinstateSubscriptionAsync(ISubscriber subscriber);
    Task<bool> CreditAccountAsync(ISubscriber subscriber, decimal creditAmount);
    Task<BillingInfo> GetBillingAsync(ISubscriber subscriber);
    Task<BillingHistoryInfo> GetBillingHistoryAsync(ISubscriber subscriber);
    Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber);
    Task<TaxInfo> GetTaxInfoAsync(ISubscriber subscriber);
    Task SaveTaxInfoAsync(ISubscriber subscriber, TaxInfo taxInfo);
    Task<string> AddSecretsManagerToSubscription(Organization org, Plan plan, int additionalSmSeats, int additionalServiceAccount);
    /// <summary>
    /// Secrets Manager Standalone is a discount in Stripe that is used to give an organization access to Secrets Manager.
    /// Usually, this also implies that when they invite a user to their organization, they are doing so for both Password
    /// Manager and Secrets Manger.
    ///
    /// This will not call out to Stripe if they don't have a GatewayId or if they don't have Secrets Manager.
    /// </summary>
    /// <param name="organization">Organization Entity</param>
    /// <returns>If the organization has Secrets Manager and has the Standalone Stripe Discount</returns>
    Task<bool> HasSecretsManagerStandalone(Organization organization);

    /// <summary>
    /// Secrets Manager Standalone is a discount in Stripe that is used to give an organization access to Secrets Manager.
    /// Usually, this also implies that when they invite a user to their organization, they are doing so for both Password
    /// Manager and Secrets Manger.
    ///
    /// This will not call out to Stripe if they don't have a GatewayId or if they don't have Secrets Manager.
    /// </summary>
    /// <param name="organization">Organization Representation used for Inviting Organization Users</param>
    /// <returns>If the organization has Secrets Manager and has the Standalone Stripe Discount</returns>
    Task<bool> HasSecretsManagerStandalone(InviteOrganization organization);
    Task<PreviewInvoiceResponseModel> PreviewInvoiceAsync(PreviewIndividualInvoiceRequestBody parameters, string gatewayCustomerId, string gatewaySubscriptionId);
    Task<PreviewInvoiceResponseModel> PreviewInvoiceAsync(PreviewOrganizationInvoiceRequestBody parameters, string gatewayCustomerId, string gatewaySubscriptionId);

}
