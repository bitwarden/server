using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Models;

public record OrganizationSubscriptionPurchase(
    OrganizationSubscriptionPurchaseMetadata Metadata,
    OrganizationPasswordManagerSubscriptionPurchase PasswordManagerSubscription,
    TokenizedPaymentSource PaymentSource,
    PlanType PlanType,
    OrganizationSecretsManagerSubscriptionPurchase SecretsManagerSubscription,
    TaxInformation TaxInformation);

public record OrganizationPasswordManagerSubscriptionPurchase(
    int Storage,
    bool PremiumAccess,
    int Seats);

public record OrganizationSecretsManagerSubscriptionPurchase(
    int Seats,
    int ServiceAccounts);

public record OrganizationSubscriptionPurchaseMetadata(
    bool FromProvider,
    bool FromSecretsManagerStandalone)
{
    public static OrganizationSubscriptionPurchaseMetadata Default => new(false, false);
}
