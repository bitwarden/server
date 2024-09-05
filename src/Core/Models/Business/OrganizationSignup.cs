using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Stripe;

namespace Bit.Core.Models.Business;

public class OrganizationSignup : OrganizationUpgrade
{
    public string Name { get; set; }
    public string BillingEmail { get; set; }
    public User Owner { get; set; }
    public string OwnerKey { get; set; }
    public string CollectionName { get; set; }
    public PaymentMethodType? PaymentMethodType { get; set; }
    public string PaymentToken { get; set; }
    public int? MaxAutoscaleSeats { get; set; } = null;
    public string InitiationPath { get; set; }

    public OrganizationSubscriptionPurchase ToSubscriptionPurchase(bool fromProvider = false)
    {
        if (!PaymentMethodType.HasValue)
        {
            return null;
        }

        var metadata = new OrganizationSubscriptionPurchaseMetadata(fromProvider, IsFromSecretsManagerTrial);

        var passwordManager = new OrganizationPasswordManagerSubscriptionPurchase(
            AdditionalStorageGb,
            PremiumAccessAddon,
            AdditionalSeats);

        var paymentSource = new TokenizedPaymentSource(PaymentMethodType.Value, PaymentToken);

        var secretsManager = new OrganizationSecretsManagerSubscriptionPurchase(
            AdditionalSmSeats ?? 0,
            AdditionalServiceAccounts ?? 0);

        var taxInformation = new TaxInformation(
            TaxInfo.BillingAddressCountry,
            TaxInfo.BillingAddressPostalCode,
            TaxInfo.TaxIdNumber,
            TaxInfo.BillingAddressLine1,
            TaxInfo.BillingAddressLine2,
            TaxInfo.BillingAddressCity,
            TaxInfo.BillingAddressState);

        return new OrganizationSubscriptionPurchase(
            metadata,
            passwordManager,
            paymentSource,
            Plan,
            UseSecretsManager ? secretsManager : null,
            taxInformation);
    }
}
