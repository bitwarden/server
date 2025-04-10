namespace Bit.Core.Billing.Models;

public record OrganizationMetadata(
    bool IsEligibleForSelfHost,
    bool IsManaged,
    bool IsOnSecretsManagerStandalone,
    bool IsSubscriptionUnpaid,
    bool HasSubscription,
    bool HasOpenInvoice,
    bool IsSubscriptionCanceled,
    DateTime? InvoiceDueDate,
    DateTime? InvoiceCreatedDate,
    DateTime? SubPeriodEndDate,
    bool IsPaymentMethodConfigured)
{
    public static OrganizationMetadata Default => new OrganizationMetadata(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        null,
        null,
        null,
        false);
}
