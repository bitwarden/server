﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Api.Requests.Accounts;
using Bit.Core.Billing.Models.Api.Requests.Organizations;
using Bit.Core.Billing.Models.Api.Responses;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Services;

public interface IPaymentService
{
    Task CancelAndRecoverChargesAsync(ISubscriber subscriber);
    Task<string> PurchaseOrganizationAsync(Organization org, PaymentMethodType paymentMethodType,
        string paymentToken, Plan plan, short additionalStorageGb, int additionalSeats,
        bool premiumAccessAddon, TaxInfo taxInfo, bool provider = false, int additionalSmSeats = 0,
        int additionalServiceAccount = 0, bool signupIsFromSecretsManagerTrial = false);
    Task<string> PurchaseOrganizationNoPaymentMethod(Organization org, Plan plan, int additionalSeats,
        bool premiumAccessAddon, int additionalSmSeats = 0, int additionalServiceAccount = 0,
        bool signupIsFromSecretsManagerTrial = false);
    Task SponsorOrganizationAsync(Organization org, OrganizationSponsorship sponsorship);
    Task RemoveOrganizationSponsorshipAsync(Organization org, OrganizationSponsorship sponsorship);
    Task<string> UpgradeFreeOrganizationAsync(Organization org, Plan plan, OrganizationUpgrade upgrade);
    Task<string> PurchasePremiumAsync(User user, PaymentMethodType paymentMethodType, string paymentToken,
        short additionalStorageGb, TaxInfo taxInfo);
    Task<string> AdjustSubscription(
        Organization organization,
        Plan updatedPlan,
        int newlyPurchasedPasswordManagerSeats,
        bool subscribedToSecretsManager,
        int? newlyPurchasedSecretsManagerSeats,
        int? newlyPurchasedAdditionalSecretsManagerServiceAccounts,
        int newlyPurchasedAdditionalStorage);
    Task<string> AdjustSeatsAsync(Organization organization, Plan plan, int additionalSeats);
    Task<string> AdjustSeats(
        Provider provider,
        Plan plan,
        int currentlySubscribedSeats,
        int newlySubscribedSeats);
    Task<string> AdjustSmSeatsAsync(Organization organization, Plan plan, int additionalSeats);
    Task<string> AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage, string storagePlanId);

    Task<string> AdjustServiceAccountsAsync(Organization organization, Plan plan, int additionalServiceAccounts);
    Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false);
    Task ReinstateSubscriptionAsync(ISubscriber subscriber);
    Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, PaymentMethodType paymentMethodType,
        string paymentToken, TaxInfo taxInfo = null);
    Task<bool> CreditAccountAsync(ISubscriber subscriber, decimal creditAmount);
    Task<BillingInfo> GetBillingAsync(ISubscriber subscriber);
    Task<BillingHistoryInfo> GetBillingHistoryAsync(ISubscriber subscriber);
    Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber);
    Task<TaxInfo> GetTaxInfoAsync(ISubscriber subscriber);
    Task SaveTaxInfoAsync(ISubscriber subscriber, TaxInfo taxInfo);
    Task<TaxRate> CreateTaxRateAsync(TaxRate taxRate);
    Task UpdateTaxRateAsync(TaxRate taxRate);
    Task ArchiveTaxRateAsync(TaxRate taxRate);
    Task<string> AddSecretsManagerToSubscription(Organization org, Plan plan, int additionalSmSeats,
        int additionalServiceAccount);
    Task<bool> RisksSubscriptionFailure(Organization organization);
    Task<bool> HasSecretsManagerStandalone(Organization organization);
    Task<(DateTime?, DateTime?)> GetSuspensionDateAsync(Stripe.Subscription subscription);
    Task<PreviewInvoiceResponseModel> PreviewInvoiceAsync(PreviewIndividualInvoiceRequestBody parameters, string gatewayCustomerId, string gatewaySubscriptionId);
    Task<PreviewInvoiceResponseModel> PreviewInvoiceAsync(PreviewOrganizationInvoiceRequestBody parameters, string gatewayCustomerId, string gatewaySubscriptionId);

}
