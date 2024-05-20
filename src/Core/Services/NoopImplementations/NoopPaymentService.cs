using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Services;

public class NoopPaymentService : IPaymentService
{
    public Task CancelAndRecoverChargesAsync(ISubscriber subscriber) => throw new NotImplementedException();

    public Task<string> PurchaseOrganizationAsync(Organization org, PaymentMethodType paymentMethodType, string paymentToken, Plan plan,
        short additionalStorageGb, int additionalSeats, bool premiumAccessAddon, TaxInfo taxInfo, bool provider = false,
        int additionalSmSeats = 0, int additionalServiceAccount = 0, bool signupIsFromSecretsManagerTrial = false) =>
        Task.FromResult(string.Empty);

    public Task SponsorOrganizationAsync(Organization org, OrganizationSponsorship sponsorship) => throw new NotImplementedException();

    public Task RemoveOrganizationSponsorshipAsync(Organization org, OrganizationSponsorship sponsorship) => throw new NotImplementedException();

    public Task<string> UpgradeFreeOrganizationAsync(Organization org, Plan plan, OrganizationUpgrade upgrade) => throw new NotImplementedException();

    public Task<string> PurchasePremiumAsync(User user, PaymentMethodType paymentMethodType, string paymentToken,
        short additionalStorageGb, TaxInfo taxInfo) =>
        throw new NotImplementedException();

    public Task<string> AdjustSubscription(Organization organization, Plan updatedPlan, int newlyPurchasedPasswordManagerSeats,
        bool subscribedToSecretsManager, int? newlyPurchasedSecretsManagerSeats,
        int? newlyPurchasedAdditionalSecretsManagerServiceAccounts, int newlyPurchasedAdditionalStorage,
        DateTime? prorationDate = null) =>
        throw new NotImplementedException();

    public Task<string> AdjustSeatsAsync(Organization organization, Plan plan, int additionalSeats, DateTime? prorationDate = null) => throw new NotImplementedException();

    public Task<string> AdjustSeats(Provider provider, Plan plan, int currentlySubscribedSeats, int newlySubscribedSeats,
        DateTime? prorationDate = null) =>
        throw new NotImplementedException();

    public Task<string> AdjustSmSeatsAsync(Organization organization, Plan plan, int additionalSeats, DateTime? prorationDate = null) => throw new NotImplementedException();

    public Task<string> AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage, string storagePlanId,
        DateTime? prorationDate = null) =>
        throw new NotImplementedException();

    public Task<string> AdjustServiceAccountsAsync(Organization organization, Plan plan, int additionalServiceAccounts,
        DateTime? prorationDate = null) =>
        throw new NotImplementedException();

    public Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false) => throw new NotImplementedException();

    public Task ReinstateSubscriptionAsync(ISubscriber subscriber) => throw new NotImplementedException();

    public Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, PaymentMethodType paymentMethodType, string paymentToken,
        TaxInfo taxInfo = null) =>
        throw new NotImplementedException();

    public Task<bool> CreditAccountAsync(ISubscriber subscriber, decimal creditAmount) => throw new NotImplementedException();

    public Task<BillingInfo> GetBillingAsync(ISubscriber subscriber) => throw new NotImplementedException();

    public Task<BillingInfo> GetBillingHistoryAsync(ISubscriber subscriber) => throw new NotImplementedException();

    public Task<BillingInfo> GetBillingBalanceAndSourceAsync(ISubscriber subscriber) => throw new NotImplementedException();

    public Task<SubscriptionInfo> GetSubscriptionAsync(ISubscriber subscriber) => throw new NotImplementedException();

    public Task<TaxInfo> GetTaxInfoAsync(ISubscriber subscriber) => throw new NotImplementedException();

    public Task SaveTaxInfoAsync(ISubscriber subscriber, TaxInfo taxInfo) => throw new NotImplementedException();

    public Task<TaxRate> CreateTaxRateAsync(TaxRate taxRate) => throw new NotImplementedException();

    public Task UpdateTaxRateAsync(TaxRate taxRate) => throw new NotImplementedException();

    public Task ArchiveTaxRateAsync(TaxRate taxRate) => throw new NotImplementedException();

    public Task<string> AddSecretsManagerToSubscription(Organization org, Plan plan, int additionalSmSeats, int additionalServiceAccount,
        DateTime? prorationDate = null) =>
        throw new NotImplementedException();

    public Task<bool> RisksSubscriptionFailure(Organization organization) => throw new NotImplementedException();
}
